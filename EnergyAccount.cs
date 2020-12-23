using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;

namespace ServiceEnergyColletor
{

    class MyContextInitializer : DropCreateDatabaseAlways<EnergyContext>
    {
        protected override void Seed(EnergyContext context)
        {
            IList<Parameter> defaultParameter = new List<Parameter>
            {
                // Price Tariff Zone
                new Parameter() { Id = "TariffZonePeakPrice", value = "1" },
                new Parameter() { Id = "TariffZoneHalfPeakPrice", value = "1" },
                new Parameter() { Id = "TariffZoneNightPrice", value = "1" },

                // Tariff Zone
                // p - Peak
                // h - Half-Peak
                // n - Night
                new Parameter() { Id = "TariffZoneMonth1", value = "nnnnnhhpphhhhhhhhpppphhn" },
                new Parameter() { Id = "TariffZoneMonth2", value = "nnnnnhhpphhhhhhhhpppphhn" },
                new Parameter() { Id = "TariffZoneMonth3", value = "nnnnnhhpphhhhhhhhhpppphn" },
                new Parameter() { Id = "TariffZoneMonth4", value = "nnnnnhhpphhhhhhhhhpppphn" },
                new Parameter() { Id = "TariffZoneMonth5", value = "nnnnnnhppphhhhhhhhhhppph" },
                new Parameter() { Id = "TariffZoneMonth6", value = "nnnnnnhppphhhhhhhhhhppph" },
                new Parameter() { Id = "TariffZoneMonth7", value = "nnnnnnhppphhhhhhhhhhppph" },
                new Parameter() { Id = "TariffZoneMonth8", value = "nnnnnnhppphhhhhhhhhhppph" },
                new Parameter() { Id = "TariffZoneMonth9", value = "nnnnnhhpphhhhhhhhhpppphn" },
                new Parameter() { Id = "TariffZoneMonth10", value = "nnnnnhhpphhhhhhhhhpppphn" },
                new Parameter() { Id = "TariffZoneMonth11", value = "nnnnnhhpphhhhhhhhpppphhn" },
                new Parameter() { Id = "TariffZoneMonth12", value = "nnnnnhhpphhhhhhhhpppphhn" }
            };

            context.Parameters.AddRange(defaultParameter);

            base.Seed(context);
        }
    }
    class EnergyContext : DbContext
    {
        public EnergyContext()
            : base("DbConnection")
        {
            //Database.SetInitializer<EnergyContext>(new MyContextInitializer());
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Facility> Facilitys { get; set; }
        public DbSet<PollChannel> PollChannels { get; set; }
        public DbSet<DailyReport> DailyReports { get; set; }
        public DbSet<Parameter> Parameters { get; set; }
    }

    public class PollChannel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(30)]
        public string Name { get; set; }

        [Required]
        [MaxLength(30)]
        public string TypeChannel { get; set; }
        public string ConnectStrJSON { get; set; }
    }

    public class Facility
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("PollChannel")]
        public int PollChannelId { get; set; }
        public PollChannel PollChannel { get; set; }
        [MaxLength(30)]
        public string Name { get; set; }
        [MaxLength(200)]
        public string Description { get; set; }
        public int Factor { get; set; }

        [Required]
        public byte NetworkAddress { get; set; }
        public bool Active { get; set; }
        public int TriggerThresholdEnergy { get; set; }
        public List<Account> Accounts { get; set; }
        public List<DailyReport> DailyReports { get; set; }
    }

    public class Account
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Facility")]
        public int FacilityId { get; set; }
        public Facility Facility { get; set; }

        [Column(TypeName = "date")]
        public DateTime ReportDate { get; set; }
        public int ReportHour { get; set; }
        public int EnergyA { get; set; }
        public int EnergyR { get; set; }
        public int WorkTime { get; set; }
        public int PollCount { get; set; }
        public int ErrPoll { get; set; }
        public DateTime tsUpdate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }

    public class DailyReport
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Facility")]
        public int FacilityId { get; set; }
        public Facility Facility { get; set; }

        [Column(TypeName = "date")]
        public DateTime ReportDate { get; set; }

        public int EnergyABegin { get; set; }
        public int EnergyRBegin { get; set; }
        public int EnergyAEnd { get; set; }
        public int EnergyREnd { get; set; }

        public int EnergyAPeak { get; set; }
        public int EnergyRPeak { get; set; }
        public int WorkTimePeak { get; set; }

        public int EnergyAHalfPeak { get; set; }
        public int EnergyRHalfPeak { get; set; }
        public int WorkTimeHalfPeak { get; set; }

        public int EnergyANight { get; set; }
        public int EnergyRNight { get; set; }
        public int WorkTimeNight { get; set; }

        public int PctSuccessfulPolls { get; set; }
    }

    public class Parameter
    {
        [Key]
        public string Id { get; set; }

        [MaxLength(200)]
        public string value { get; set; }
    }

}
