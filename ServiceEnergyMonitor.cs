using System.Diagnostics;
using System.ServiceProcess;
using System.Timers;
using System.Runtime.InteropServices;
using System.Linq;
using CounterMercury23x;
using Newtonsoft.Json;
using System;

namespace ServiceEnergyColletor
{

    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public int dwServiceType;
        public ServiceState dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    };

    public partial class ServiceEnergyMonitor : ServiceBase
    {

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);


        private const int POLLING_INTERVAL_MINUTES = 5; 
        private EventLog eventLog;
        private Timer timer;
        public ServiceEnergyMonitor()
        {
            InitializeComponent();
            this.CanPauseAndContinue = false; // службу нельзя приостановить и затем продолжить
            this.CanStop = true; // службу можно остановить

            eventLog = new System.Diagnostics.EventLog();
            if (!EventLog.SourceExists("EnergyMonitor"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "EnergyMonitor", "Log");
            }
            eventLog.Source = "EnergyMonitor";
            eventLog.Log = "Log";
        }

        protected override void OnStart(string[] args)
        {
            // Update the service state to Start Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Pre start
            int wait = (POLLING_INTERVAL_MINUTES - DateTime.Now.Minute % POLLING_INTERVAL_MINUTES) * 60 - DateTime.Now.Second;
            this.timer = new Timer
            {
                Interval = wait * 1000 // 1 seconds
            };
            this.timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            this.timer.Start();

            // Update the service state to Running.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_RUNNING;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLog.WriteEntry("Service start.");
        }

        private static void makeDailyReport(EnergyContext db, DateTime ADate, int cointerId)
        {
            string qTarifZoneName = "TariffZoneMonth" + ADate.Month.ToString();
            var TariffZoneMonth = db.Parameters.FirstOrDefault(q => q.Id == qTarifZoneName);
            if (TariffZoneMonth != null)
            {
                int EnergyABegin = 0;
                int EnergyRBegin = 0;
                int EnergyAEnd = 0;
                int EnergyREnd = 0;

                int EnergyAPeak = 0;
                int EnergyRPeak = 0;
                int WorkTimePeak = 0;

                int EnergyAHalfPeak = 0;
                int EnergyRHalfPeak = 0;
                int WorkTimeHalfPeak = 0;

                int EnergyANight = 0;
                int EnergyRNight = 0;
                int WorkTimeNight = 0;

                int PollsCount = (60 / POLLING_INTERVAL_MINUTES) * 24;
                int PollsErrCount = 0;
                int PctSuccessfulPolls = 0;

                var accDay = db.Accounts
                            .Where(q =>
                               q.ReportDate == ADate.Date &&
                               q.FacilityId == cointerId
                            )
                            .OrderBy(q => q.ReportHour)
                            .ToList();

                if (accDay.Count() > 0)
                {
                    int zoneHour = 0;
                    int OffsetEnergyA = 0;
                    int OffsetEnergyR = 0;

                    int calcEnergyA = 0;
                    int calcEnergyR = 0;
                    char zone = ' ';

                    foreach (Account acc in accDay)
                    { 
                        if (acc.EnergyA != 0)
                        {
                            if (EnergyABegin == 0) 
                            {
                                EnergyABegin = acc.EnergyA;
                                EnergyRBegin = acc.EnergyR;
                            }

                            if (OffsetEnergyA == 0)
                            {
                                OffsetEnergyA = acc.EnergyA;
                                OffsetEnergyR = acc.EnergyR;
                            }
                            
                        }

                        zoneHour = acc.ReportHour;
                        if (zoneHour == 0) continue;

                        zone = TariffZoneMonth.value[zoneHour-1];
                        calcEnergyA = acc.EnergyA - OffsetEnergyA;
                        calcEnergyR = acc.EnergyR - OffsetEnergyR;

                        if (zone == 'p')
                        {
                            EnergyAPeak += calcEnergyA;
                            EnergyRPeak += calcEnergyR;
                            WorkTimePeak += acc.WorkTime;
                        }

                        if (zone == 'h')
                        {
                            EnergyAHalfPeak += calcEnergyA;
                            EnergyRHalfPeak += calcEnergyR;
                            WorkTimeHalfPeak += acc.WorkTime;
                        }

                        if (zone == 'n')
                        {
                            EnergyANight += calcEnergyA;
                            EnergyRNight += calcEnergyR;
                            WorkTimeNight += acc.WorkTime;
                        }

                        PollsErrCount += acc.ErrPoll;
                        OffsetEnergyA = acc.EnergyA;
                        OffsetEnergyR = acc.EnergyR;

                    }

                    EnergyAEnd = OffsetEnergyA;
                    EnergyREnd = OffsetEnergyR;
                    PctSuccessfulPolls = 100 - (PollsErrCount * 100/ PollsCount);

                    var DailyReports = db.DailyReports.FirstOrDefault(q =>
                                           q.FacilityId == cointerId &&
                                           q.ReportDate == ADate.Date
                                           );

                    if (DailyReports != null)
                    {
                        DailyReports.EnergyABegin = EnergyABegin;
                        DailyReports.EnergyRBegin = EnergyRBegin;
                        DailyReports.EnergyAEnd = EnergyAEnd;
                        DailyReports.EnergyREnd = EnergyREnd;
                        DailyReports.EnergyAPeak = EnergyAPeak;
                        DailyReports.EnergyRPeak = EnergyRPeak;
                        DailyReports.WorkTimePeak = WorkTimePeak;
                        DailyReports.EnergyAHalfPeak = EnergyAHalfPeak;
                        DailyReports.EnergyRHalfPeak = EnergyRHalfPeak;
                        DailyReports.WorkTimeHalfPeak = WorkTimeHalfPeak;
                        DailyReports.EnergyANight = EnergyANight;
                        DailyReports.EnergyRNight = EnergyRNight;
                        DailyReports.WorkTimeNight = WorkTimeNight;
                        DailyReports.PctSuccessfulPolls = PctSuccessfulPolls;
                    }
                    else
                    {
                        DailyReport DR = new DailyReport
                        {
                            FacilityId = cointerId,
                            ReportDate = ADate.Date,
                            EnergyABegin = EnergyABegin,
                            EnergyRBegin = EnergyRBegin,
                            EnergyAEnd = EnergyAEnd,
                            EnergyREnd = EnergyREnd,
                            EnergyAPeak = EnergyAPeak,
                            EnergyRPeak = EnergyRPeak,
                            WorkTimePeak = WorkTimePeak,
                            EnergyAHalfPeak = EnergyAHalfPeak,
                            EnergyRHalfPeak = EnergyRHalfPeak,
                            WorkTimeHalfPeak = WorkTimeHalfPeak,
                            EnergyANight = EnergyANight,
                            EnergyRNight = EnergyRNight,
                            WorkTimeNight = WorkTimeNight,
                            PctSuccessfulPolls = PctSuccessfulPolls,
                        };

                        db.DailyReports.Add(DR);
                    }

                    db.SaveChanges();
                }
                
            }
            else
            {
                throw new Exception("Dayli report. Not find Tariff Zone Month = " + qTarifZoneName);
            }
        }

        private static void TaskDailyReport(EnergyContext db, int cointerId)
        {
            var lastReport = db.DailyReports
                .OrderByDescending(o => o.Id)
                .FirstOrDefault(q =>
                    q.FacilityId == cointerId
                );

            if (lastReport != null)
            {
                DateTime dt = DateTime.Now;
                DateTime reportDate = lastReport.ReportDate.Date;
                
                while(reportDate < dt.Date)
                {
                    reportDate = reportDate.AddDays(1);
                    makeDailyReport(db, reportDate, cointerId);
                }
            }
        }


        private static Account makeTempleteAccount(int cointerId, DateTime ADate) => new Account
            {
                FacilityId = cointerId,
                ReportDate = ADate.Date,
                ReportHour = ADate.Hour,
                EnergyA = 0,
                EnergyR = 0,
                WorkTime = 0,
                PollCount = 1,
                ErrPoll = 0,
                tsUpdate = DateTime.Now
            };


        private static void setDBAccountCounter(EnergyContext db, int cointerId, EnergyCounter ecount, int EnergyFactor, int EnergyMinIsWork, bool isPolling)
        {
            DateTime curDate = DateTime.Now;        // Текущая дата
            DateTime accDate = curDate.AddHours(1); // Отчетное время

            bool isWorking = false;
            int preEnergy = 0;

            var curCount = db.Accounts.FirstOrDefault(q =>
                q.FacilityId == cointerId &&
                q.ReportDate == curDate.Date &&
                q.ReportHour == curDate.Hour
            );

            var accCount = db.Accounts.FirstOrDefault(q =>
                q.FacilityId == cointerId &&
                q.ReportDate == accDate.Date &&
                q.ReportHour == accDate.Hour
            );

            if (curCount != null) preEnergy = curCount.EnergyA;
            if (accCount != null) preEnergy = accCount.EnergyA;

            int deltaEnergy = (int)((ecount.EnergyA - preEnergy) * EnergyFactor);
            if (deltaEnergy > EnergyMinIsWork) isWorking = true;

            if (accCount == null)
            {
                Account accNewCount = makeTempleteAccount(cointerId, accDate);
                if (isPolling)
                {
                    accNewCount.EnergyA = (int)ecount.EnergyA;
                    accNewCount.EnergyR = (int)ecount.EnergyR;
                }
                else
                {
                    accNewCount.ErrPoll = 1;
                }

                db.Accounts.Add(accNewCount);

                if (curCount != null)
                {
                    if (curCount.tsUpdate.Hour != accDate.Hour)
                    {
                        if (isPolling)
                        {
                            curCount.EnergyA = (int)ecount.EnergyA;
                            curCount.EnergyR = (int)ecount.EnergyR;
                        }
                        curCount.tsUpdate = curDate;
                    }
                }
            }
            else
            {  
                if (isPolling) 
                {
                    accCount.EnergyA = (int)ecount.EnergyA;
                    accCount.EnergyR = (int)ecount.EnergyR;
                } 
                else 
                {
                    accCount.ErrPoll++;
                }
                accCount.PollCount++;
                if (isWorking) accCount.WorkTime += POLLING_INTERVAL_MINUTES;
                accCount.tsUpdate = curDate;
            }


            if (curCount == null && isPolling)
            {
                Account accNewCurCount = makeTempleteAccount(cointerId, curDate);
                accNewCurCount.EnergyA = (int)ecount.EnergyA;
                accNewCurCount.EnergyR = (int)ecount.EnergyR;
                db.Accounts.Add(accNewCurCount);
            }

        }
        
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            // TODO: Insert monitoring activities here.
            this.timer.Stop();

            eventLog.WriteEntry("Start polling. Connect to database.", EventLogEntryType.Information);

            try
            {

                using (EnergyContext db = new EnergyContext())
                {

                    var Channels = db.PollChannels.ToList();

                    if (Channels.Count != 0)
                    {

                        foreach (PollChannel channel in Channels)
                        {
                            IMercury23xChannel port = null;

                            // Создаем канал.
                            if (channel.TypeChannel == "SerialPort")
                            {
                                SerialPortAdapterConfig config = JsonConvert.DeserializeObject<SerialPortAdapterConfig>(channel.ConnectStrJSON);
                                port = new SerialPortAdapter(config);
                            }

                            var Facilitys = db.Facilitys
                                .Where(f =>
                                    f.Active == true &&
                                    f.PollChannelId == channel.Id)
                                .ToList();

                            if (Facilitys.Count() != 0 && port != null)
                            {
                                try
                                {
                                    port.Open();

                                    eventLog.WriteEntry(
                                                String.Format("Port open, id = {0}, name = {1}. Start polling.", channel.Id, channel.Name),
                                                EventLogEntryType.Information
                                            );

                                    foreach (Facility counter in Facilitys)
                                    {
                                        EnergyCounter ECounter = new EnergyCounter { EnergyA = 0, EnergyR = 0 };
                                        bool isPolling = false;

                                        try
                                        {
                                            Mercury23x.SessionOpenRead(counter.NetworkAddress, port.sendPacket);
                                            ECounter = Mercury23x.GetReadingsEnergy(counter.NetworkAddress, port.sendPacket);
                                            isPolling = true;
                                            Mercury23x.SessionClose(counter.NetworkAddress, port.sendPacket);
                                        }
                                        catch (Exception)
                                        {
                                            eventLog.WriteEntry(
                                                String.Format("Counter has #Id in database = {0}, name = {1} Not Found or error polling.", counter.Id, counter.Name),
                                                EventLogEntryType.Error
                                            );

                                        }

                                        setDBAccountCounter(db, counter.Id, ECounter, counter.Factor, counter.TriggerThresholdEnergy, isPolling);
                                        db.SaveChanges();

                                        try
                                        {
                                            if (DateTime.Now.Minute < POLLING_INTERVAL_MINUTES*2) TaskDailyReport(db, counter.Id);
                                        }
                                        catch (Exception e)
                                        {
                                            eventLog.WriteEntry(
                                                String.Format("Error DailyReport. counter id = {0}. Exception: {1}", counter.Id, e),
                                                EventLogEntryType.Error
                                            );
                                        };



                                    }
                                }
                                catch (Exception e)
                                {
                                    eventLog.WriteEntry(
                                                String.Format("Port error id = {0}, name = {1}. Exception: {2}", channel.Id, channel.Name, e),
                                                EventLogEntryType.Error
                                            );
                                }
                                finally
                                {
                                    port.Close();
                                }

                            }
                            else
                            {
                                eventLog.WriteEntry(
                                                String.Format("There are no active polling devices in this channel. id = {0}, name = {1}", channel.Id, channel.Name),
                                                EventLogEntryType.Warning
                                            );
                            }
                        }

                    }
                    else
                    {
                        eventLog.WriteEntry(
                                                "Not find any channel for polling",
                                                EventLogEntryType.Warning
                                            );
                    }

                }
            }
            catch (Exception e)
            {
                eventLog.WriteEntry(
                                     String.Format("Unknow error. Exception: {0}", e),
                                     EventLogEntryType.Error
                                   );
            }
            eventLog.WriteEntry("End polling.", EventLogEntryType.Information);

            int wait = (POLLING_INTERVAL_MINUTES - DateTime.Now.Minute % POLLING_INTERVAL_MINUTES) * 60 - DateTime.Now.Second;
            this.timer.Interval = wait * 1000;
            this.timer.Start();
        }

        protected override void OnStop()
        {
            // Update the service state to Stop Pending.
            ServiceStatus serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_STOP_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Pre stop
            this.timer.Stop();
            this.timer.Close();

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            eventLog.WriteEntry("Service stop.");
        }
    }


}
