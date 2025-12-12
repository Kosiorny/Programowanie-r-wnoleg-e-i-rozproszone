using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KopalniaZad3
{
    class Program
    {
        // PARAMETRY SYMULACJI
        const int InitialCoal = 2000;
        const int TruckCapacity = 200;
        const int MineTimePerUnit = 10;      // ms
        const int WarehouseTimePerUnit = 10; // ms
        const int TransportTime = 10_000;    // ms

        static SemaphoreSlim mineSemaphore = new SemaphoreSlim(2, 2);
        static SemaphoreSlim warehouseSemaphore = new SemaphoreSlim(1, 1);

        static int coalDeposit;
        static int warehouse;
        static object lockObj = new object();

        static void Main()
        {
            double timeForOneMiner = 0;

            for (int miners = 1; miners <= 6; miners++)
            {
                ResetState();

                Stopwatch sw = Stopwatch.StartNew();

                Task[] tasks = Enumerable
                    .Range(1, miners)
                    .Select(id => Task.Run(() => MinerWork(id)))
                    .ToArray();

                Task.WaitAll(tasks);
                sw.Stop();

                double timeSec = sw.Elapsed.TotalSeconds;

                if (miners == 1)
                    timeForOneMiner = timeSec;

                double speedup = timeForOneMiner / timeSec;
                double efficiency = speedup / miners;

                Console.WriteLine(
                    $"liczba górników: {miners}, " +
                    $"czas: {timeSec:F2} s, " +
                    $"przyśpieszenie: {speedup:F2}, " +
                    $"efektywność: {efficiency:F2}"
                );
            }
        }

        static void ResetState()
        {
            coalDeposit = InitialCoal;
            warehouse = 0;
        }

        static void MinerWork(int id)
        {
            while (true)
            {
                int loaded = 0;

                // WYDOBYCIE
                mineSemaphore.Wait();

                while (loaded < TruckCapacity)
                {
                    lock (lockObj)
                    {
                        if (coalDeposit <= 0)
                        {
                            mineSemaphore.Release();
                            return;
                        }

                        coalDeposit--;
                        loaded++;
                    }

                    Thread.Sleep(MineTimePerUnit);
                }

                mineSemaphore.Release();

                // TRANSPORT
                Thread.Sleep(TransportTime);

                // ROZŁADUNEK
                warehouseSemaphore.Wait();

                for (int i = 0; i < loaded; i++)
                {
                    Thread.Sleep(WarehouseTimePerUnit);
                    lock (lockObj)
                    {
                        warehouse++;
                    }
                }

                warehouseSemaphore.Release();
            }
        }
    }
}
