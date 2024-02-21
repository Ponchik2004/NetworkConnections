using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NetworkConnections
{
    internal class Program
    {
        // Структура, яка представляє один рядок таблиці TCP
        [StructLayout(LayoutKind.Sequential)]
        public struct TcpRow
        {
            public TcpState state;
            public uint localAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] localPort;
            public uint remoteAddr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] remotePort;
            public int owningPid;
        }

        // Перелік типів таблиць TCP
        public enum TcpTableClass
        {
            TcpTableBasicListener,
            TcpTableBasicConnections,
            TcpTableBasicAll,
            TcpTableOwnerPidListener,
            TcpTableOwnerPidConnections,
            TcpTableOwnerPidAll,
            TcpTableOwnerModuleListener,
            TcpTableOwnerModuleConnections,
            TcpTableOwnerModuleAll
        }

        // Функція, яка викликає GetExtendedTcpTable з Windows API
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, TcpTableClass tblClass, int reserved);

        // Функція, яка повертає масив TcpRow з таблиці TCP
        public static TcpRow[] GetTcpTable()
        {
            int bufferSize = 0;
            uint result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll, 0);
            if (result != 0 && result != 122) // 122 means insufficient buffer size
            {
                throw new Exception("Error calling GetExtendedTcpTable: " + result);
            }
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                result = GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll, 0);
                if (result != 0)
                {
                    throw new Exception("Error calling GetExtendedTcpTable: " + result);
                }
                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = (IntPtr)((long)buffer + 4);
                TcpRow[] tcpRows = new TcpRow[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    tcpRows[i] = (TcpRow)Marshal.PtrToStructure(rowPtr, typeof(TcpRow));
                    rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(typeof(TcpRow)));
                }
                return tcpRows;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static void Main(string[] args)
        {
            // Отримуємо масив TcpRow з таблиці TCP
            TcpRow[] tcpRows = GetTcpTable();

            // Для помилок
            string errors = "";

            // Виводимо заголовок таблиці
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");
            Console.WriteLine("|  Local Address  | Local Port | Remote Address  | Remote Port |       Process Name        |      State      |");
            Console.WriteLine("--------------------------------------------------------------------------------------------------------------");

            // Виводимо результати
            foreach (TcpRow tcpRow in tcpRows)
            {
                // Конвертуємо адреси і порти в зручний формат
                string localAddress = new IPAddress(tcpRow.localAddr).ToString();
                int localPort = BitConverter.ToUInt16(new byte[2] { tcpRow.localPort[1], tcpRow.localPort[0] }, 0);
                string remoteAddress = new IPAddress(tcpRow.remoteAddr).ToString();
                int remotePort = BitConverter.ToUInt16(new byte[2] { tcpRow.remotePort[1], tcpRow.remotePort[0] }, 0);
                string state = tcpRow.state.ToString();
                // Отримуємо ім'я процесу за його ідентифікатором
                string processName = "N/A";
                try
                {
                    processName = Process.GetProcessById(tcpRow.owningPid).ProcessName;
                }
                catch (Exception ex)
                {
                    // Обробка випадку, коли не вдається отримати ім'я процесу
                    errors += ($"\nError getting process name for PID {tcpRow.owningPid}: {ex.Message}");
                }

                // Виводимо інформацію про кожне підключення
                Console.WriteLine($"| {localAddress,-15} | {localPort,-10} | {remoteAddress,-15} | {remotePort,-11} | {processName,-25} | {state,-15} |");
            }

            Console.WriteLine("------------------------------------------------------------------------------------------------------------");
            if(errors == "")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("There are no errors");
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errors);
            Console.ReadLine();
        }

    }
}
