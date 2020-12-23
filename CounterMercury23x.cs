using System;
using System.IO.Ports;

/*
    Error code
    -----------------------------------
    EC1000: Mercury crc exception			- Ошибка котрольной суммы 
    EC1001: Invalid command or parameter	- Недопустимая команда или параметр
    EC1002: Internal counter error			- Внутренняя ошибка счетчика 
    EC1003: Insufficient access level		- Недостаточен уровень доступа 
    EC1004: Internal clock was adjusted		- Внутренние часы корректировались
    EC1005: Communication channel not open	- Не открыт канал связи
    EC1006: Invalid packet					- Пакет поврежден.
    EC1007: Password empty or invalid lenght - Неверная длина пароля.
 */

namespace CounterMercury23x
{

    public class MercuryException : Exception
    {
        public MercuryException() : base() { }
        public MercuryException(string message) : base(message) { }

    }

    public struct EnergyCounter
    {
        public UInt32 EnergyA;  // энергия активная прямая (NA+), Вт*ч
        public UInt32 EnergyR;  // энергия реактивная прямая (NR+), вар*ч
    }

    /// <summary>
    /// Implementation of the basic protocol of the Mercury 23x counter
    /// </summary>
    static class Mercury23xBaseProtocol
    {
        private const int PACKETLEN_ENERGY = 19;
        private const int PACKETLEN_MINIMAL = 4;
        private const int PASSWORD_LEN = 6;
        public const int PACKETSIZE_READ_AR_ENERGY = 19;
        public const int PACKETSIZE_STATUS = 4;

        private static UInt16 CalcCRC(byte[] buf, int len)
        {
            UInt16 crc = 0xFFFF;

            for (int pos = 0; pos < len; pos++)
            {
                crc ^= (UInt16)buf[pos];          // XOR byte into least sig. byte of crc

                for (int i = 8; i != 0; i--)
                {    // Loop over each bit
                    if ((crc & 0x0001) != 0)
                    {      // If the LSB is set
                        crc >>= 1;                    // Shift right and XOR 0xA001
                        crc ^= 0xA001;
                    }
                    else                            // Else LSB is not set
                        crc >>= 1;                    // Just shift right
                }
            }
            // Note, this number has low and high bytes swapped, so use it accordingly (or swap bytes)
            return crc;
        }

        /// <summary>
        /// Add CRC
        /// </summary>
        /// <param name="buf">Command</param>
        /// <returns></returns>
        public static byte[] AddCRC(byte[] buf)
        {
            int bufLen = buf.Length;
            byte[] crc = BitConverter.GetBytes(CalcCRC(buf, bufLen));
            byte[] resultByteArr = new byte[bufLen + 2];

            // copy array
            for (int i = 0; i < bufLen; i++)
            {
                resultByteArr[i] = buf[i];
            }
            // Add crc
            resultByteArr[bufLen] = crc[0];
            resultByteArr[bufLen + 1] = crc[1];

            return resultByteArr;
        }

        /// <summary>
        /// Create request check counter
        /// </summary>
        /// <param name="address">Counter(device) address</param>
        /// <returns>Request</returns>
        public static byte[] RequestCheckCounter(byte address)
        {
            return AddCRC(new byte[2] { address, 0x00 });
        }

        /// <summary>
        /// Create request open new session for user. Only read
        /// </summary>
        /// <param name="address">Counter(device) address</param>
        /// <returns>Request</returns>
        public static byte[] RequestOpenSessionRead(byte address)
        {
            return AddCRC(new byte[9] { address, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 });
        }

        /// <summary>
        /// Create request open new session
        /// </summary>
        /// <param name="address">Counter(device) address</param>
        /// <param name="accessLeven">Access level</param>
        /// <param name="password">Password - 6 byte</param>
        /// <returns>Request</returns>
        public static byte[] RequestOpenSession(byte address, byte accessLeven, byte[] password)
        {
            if (password.Length != PASSWORD_LEN)
            {
                throw new MercuryException("EC1007: Password empty or invalid lenght");
            }

            return AddCRC(new byte[9] {
                address,
                0x01,
                accessLeven,
                password[0],
                password[1],
                password[2],
                password[3],
                password[4],
                password[5],
            });
        }

        /// <summary>
        /// Create request close session
        /// </summary>
        /// <param name="address">Counter(device) address</param>
        /// <returns>Request</returns>
        public static byte[] RequestCloseSession(byte address)
        {
            return AddCRC(new byte[2] { address, 0x02 });
        }

        /// <summary>
        /// Create request to get the counter reading
        /// </summary>
        /// <param name="address">Counter(device) address</param>
        /// <returns>Request</returns>
        public static byte[] RequestReadCountEnergy(byte address)
        {
            return AddCRC(new byte[4] { address, 0x05, 0x00, 0x00 });
        }

        /// <summary>
        /// Parser electricity meter
        /// </summary>
        /// <param name="bufResponse">Byte buffer</param>
        /// <returns>Count energy</returns>
        public static EnergyCounter GetEnergy(byte[] bufResponse)
        {
            return GetEnergy(bufResponse, bufResponse.Length);
        }

        /// <summary>
        /// arser electricity meter
        /// </summary>
        /// <param name="bufResponse">Byte buffer</param>
        /// <param name="BufLen">lenght buffer</param>
        /// <returns></returns>
        public static EnergyCounter GetEnergy(byte[] bufResponse, int BufLen)
        {
            CheckCRC(bufResponse, BufLen);

            EnergyCounter ec = new EnergyCounter();

            if (BufLen == PACKETLEN_ENERGY)
            {
                ec.EnergyA = BitConverter.ToUInt32(
                    new byte[4] {
                        bufResponse[3],
                        bufResponse[4],
                        bufResponse[1],
                        bufResponse[2]
                    }
                    , 0);
                ec.EnergyR = BitConverter.ToUInt32(
                    new byte[4] {
                        bufResponse[11],
                        bufResponse[12],
                        bufResponse[9],
                        bufResponse[10]
                    }
                    , 0);
            }

            return ec;
        }

        /// <summary>
        /// Check crc in response
        /// </summary>
        /// <param name="buf">Byte buffer</param>
        public static void CheckCRC(byte[] buf)
        {
            CheckCRC(buf, buf.Length);
        }

        /// <summary>
        /// Check crc in response
        /// </summary>
        /// <param name="buf">Byte buffer</param>
        /// <param name="BufLen">Lenght buffer</param>
        public static void CheckCRC(byte[] buf, int BufLen)
        {
            if (BufLen < PACKETLEN_MINIMAL) throw new MercuryException("EC1006: Invalid packet");
            //chech CRC
            byte[] cCRC = BitConverter.GetBytes(CalcCRC(buf, BufLen - 2));
            if ((buf[BufLen - 2] != cCRC[0]) || (buf[BufLen - 1] != cCRC[1]))
            {
                throw new MercuryException("EC1000: Mercury CRC exception");
            }
        }

        /// <summary>
        /// Parcer state counter 
        /// </summary>
        /// <param name="buf">Byte buffer</param>
        /// <param name="BufLen">lenght buffer</param>
        public static void CheckState(byte[] buf, int BufLen)
        {
            CheckCRC(buf, BufLen);

            if (buf[1] != 0)
            {
                switch (buf[1])
                {
                    case 1: throw new MercuryException("EC1001: Invalid command or parameter");
                    case 2: throw new MercuryException("EC1002: Internal counter error");
                    case 3: throw new MercuryException("EC1003: Insufficient access level");
                    case 4: throw new MercuryException("EC1004: Internal clock was adjusted");
                    case 5: throw new MercuryException("EC1005: Communication channel not open");
                }
            }
        }
        /// <summary>
        /// Parcer state counter
        /// </summary>
        /// <param name="buf">Byte buffer</param>
        public static void CheckState(byte[] buf)
        {
            CheckState(buf, buf.Length);
        }


    }

    public interface IMercury23xChannel
    {
        bool IsOpen { get; }
        void Open();
        void Close();
        byte[] sendPacket(byte[] packet, int readBytes);

    };

    public struct SerialPortAdapterConfig
    {
        public string portName;
        public int baudRate;
    }

    public class SerialPortAdapter: IMercury23xChannel
    {
        private SerialPort _port;

        public SerialPortAdapter(SerialPortAdapterConfig config) 
            :this(config.portName, config.baudRate)
        { }     
        
        public SerialPortAdapter(string portName, int baudRate)
        {
            this._port = new SerialPort(portName, baudRate)
            {
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = Parity.None,
                ReadTimeout = 500,
                WriteTimeout = 500,
                ReadBufferSize = 20,
                WriteBufferSize = 20
            };
        }

        public bool IsOpen => this._port.IsOpen;

        public void Close()
        {
            this._port.Close();
        }

        public void Open()
        {
            this._port.Open();
        }

        /// <summary>
        /// Send command device
        /// </summary>
        /// <param name="packet">arraybytes of command</param>
        /// <param name="readBytes">Size response or if 0 - not response</param>
        /// <returns></returns>
        public byte[] sendPacket(byte[] packet, int readBytes)
        {
            
            if (!this.IsOpen) 
            {
                throw new System.InvalidOperationException("The specified port is not open");
            }
            
            int sendBytes = packet.Length;
            byte[] buffer = new byte[readBytes];
            string log = "";

            try
            {
                // if there are any unread bytes in the read buffer, they are junk
                //  read them now so the buffer is clear to receive.
                while (this._port.BytesToRead > 0)
                    this._port.ReadByte();

                log += " TX[" + sendBytes.ToString() + "]={ " + BitConverter.ToString(packet);
                this._port.Write(packet, 0, sendBytes);
                log += " };  RX[" + readBytes.ToString() + "]={ ";

                int count = readBytes;
                int offset = 0;
                int readCount = 0;
                while (count > 0)
                {
                    readCount = _port.Read(buffer, offset, count);
                    offset += readCount;
                    count -= readCount;
                }

                log += BitConverter.ToString(buffer) + " }";
            }

            catch (Exception)
            {
                log += " *TIMEOUT*";
                throw new TimeoutException(log);
            }

            return buffer;
        }
    }


    public delegate byte[] sendPacket(byte[] packet, int readBytes);

    public static class Mercury23x
    {

        public static void SessionOpenRead(byte netAddress, sendPacket send)
        {
            Mercury23xBaseProtocol.CheckState(
                send(
                    Mercury23xBaseProtocol.RequestOpenSessionRead(netAddress), 
                    Mercury23xBaseProtocol.PACKETSIZE_STATUS
                    )
                );
        }
        public static void SessionClose(byte netAddress, sendPacket send)
        {
            Mercury23xBaseProtocol.CheckState(
                send(
                    Mercury23xBaseProtocol.RequestCloseSession(netAddress), 
                    Mercury23xBaseProtocol.PACKETSIZE_STATUS
                    )
                );
        }

        /// <summary>
        /// Get the current electricity reading from the meter - Active and Reactive
        /// </summary>
        /// <param name="netAddress">Counter network address</param>
        /// <param name="send">delegate sendPacket</param>
        /// <returns>responce Energy</returns>
        public static EnergyCounter GetReadingsEnergy(byte netAddress, sendPacket send)
        {
            EnergyCounter ECounter = Mercury23xBaseProtocol.GetEnergy(
                send(
                    Mercury23xBaseProtocol.RequestReadCountEnergy(netAddress), 
                    Mercury23xBaseProtocol.PACKETSIZE_READ_AR_ENERGY
                    )
                );
            return ECounter;
        }
    }

    
}
