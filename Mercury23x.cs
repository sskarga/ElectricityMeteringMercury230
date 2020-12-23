using System;


namespace CounterMercury23x
{
    public class MercuryException : Exception
    {
        public MercuryException() : base() { }
        public MercuryException(string message) : base(message) { }

    }

    public struct EnergyCounter
    {
        public float AEnergy;  // энергия активная прямая (NA+), Вт*ч
        public float REnergy;  // энергия реактивная прямая (NR+), вар*ч
    }

    static class Mercury23x
    {
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

        private static byte[] AddCRC(byte[] buf)
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
            resultByteArr[bufLen] = crc[1];
            resultByteArr[bufLen + 1] = crc[0];

            return resultByteArr;
        }

        public static byte[] QueryCheckCounter(byte address)
        {
            return AddCRC(new byte[2] { address, 0x00 });
        }

        public static byte[] QueryOpenChannelRead(byte address)
        {
            return AddCRC(new byte[8] { address, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01 });
        }

        public static byte[] QueryCloseChannel(byte address)
        {
            return AddCRC(new byte[2] { address, 0x02 });
        }

        public static byte[] QueryReadCountEnergy(byte address)
        {
            return AddCRC(new byte[4] { address, 0x05, 0x00, 0x00 });
        }

        public static EnergyCounter getEnergy(byte[] bufResponse)
        {
            CheckCRC(ref bufResponse);

            EnergyCounter ec = new EnergyCounter();

            if (bufResponse.Length == 18)
            {
                CheckResponceState(ref bufResponse);
                ec.AEnergy = BitConverter.ToSingle(
                    new byte[4] {
                        bufResponse[2],
                        bufResponse[1],
                        bufResponse[4],
                        bufResponse[3]
                    }
                    , 0);
                ec.REnergy = BitConverter.ToSingle(
                    new byte[4] {
                        bufResponse[10],
                        bufResponse[9],
                        bufResponse[12],
                        bufResponse[11]
                    }
                    , 0);
            }

            return ec;
        }

        public static void CheckCRC(ref byte[] buf)
        {
            int resBufLen = buf.Length;

            if (resBufLen < 4) throw new MercuryException("EC1006: Invalid packet");
            //chech CRC
            byte[] cCRC = BitConverter.GetBytes(CalcCRC(buf, resBufLen - 2));
            if ((buf[resBufLen - 2] != cCRC[1]) || (buf[resBufLen - 1] != cCRC[0]))
            {
                throw new MercuryException("EC1000: Mercury CRC exception");
            }
        }

        public static void CheckResponceState(ref byte[] buf)
        {
            CheckCRC(ref buf);

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
    }
};