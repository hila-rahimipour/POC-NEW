﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Data;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;


namespace POC_NEW
{
    class Program
    {
        static int tableWidth = 170;
        [DllImport("Kernel32.dll"), SuppressUnmanagedCodeSecurity]
        public static extern int GetCurrentProcessorNumber();
        public static void PrintLine()
        {
            Console.WriteLine(new string('-', tableWidth));
        }
        public static void PrintRow(params string[] columns)
        {
            int width = (tableWidth - columns.Length) / columns.Length;
            string row = "|";

            foreach (string column in columns)
            {
                row += AlignCentre(column, width) + "|";
            }

            Console.WriteLine(row);
        }
        public static string AlignCentre(string text, int width)
        {
            text = text.Length > width ? text.Substring(0, width - 3) + "..." : text;

            if (string.IsNullOrEmpty(text))
            {
                return new string(' ', width);
            }
            else
            {
                return text.PadRight(width - (width - text.Length) / 2).PadLeft(width);
            }
        }
        public static void OneProcess(Dictionary<int, object[]> info, Process process)
        {
            Console.Clear();

            object[] collected_info;
            //0- id, 1- cpu, 2- physical memory, 3- ram, 4- network, 5- total cpu, 6- total memory, 7- total ram, 8- total network, 9- thread
            try
            {
                collected_info = info[process.Id];
            }
            catch
            {
                collected_info = new object[10];
            }

            //get cpu info
            PerformanceCounter myAppCpu = new PerformanceCounter("Process", "% Processor Time", process.ProcessName);
            myAppCpu.NextValue();
            Thread.Sleep(10);
            double cpu = myAppCpu.NextValue();

            //get private working set memory info
            PerformanceCounter PC = new PerformanceCounter();
            PC.CategoryName = "Process";
            PC.CounterName = "Working Set - Private";
            PC.InstanceName = process.ProcessName;
            PC.NextValue();
            Thread.Sleep(10);
            float ram_usage = PC.NextValue() / 1024;
            PC.Close();
            PC.Dispose();

            //get private memory info
            long memory = process.PrivateMemorySize64;

            //get netork info
            PerformanceCounter net = new PerformanceCounter("Process", "IO Read Bytes/sec", process.ProcessName);
            net.NextValue();
            Thread.Sleep(10);
            double network = net.NextValue();

            //updating data
            collected_info[0] = process.ProcessName;
            collected_info[1] = cpu / 10;
            collected_info[2] = memory;
            collected_info[3] = ram_usage;
            collected_info[4] = network;

            if (collected_info[5] == null)
                collected_info[5] = (double)0;
            else
            {
                double total_cpu = (double)collected_info[5] + cpu;
                collected_info[5] = total_cpu;
            }

            if (collected_info[6] == null)
                collected_info[6] = (long)0;
            else
            {
                long total_memory = (long)collected_info[6] + memory;
                collected_info[6] = total_memory;
            }

            if (collected_info[7] == null)
                collected_info[7] = (float)0;
            else
            {
                float total_ram = (float)collected_info[7] + ram_usage;
                collected_info[7] = total_ram;
            }
            if (collected_info[8] == null)
                collected_info[8] = (double)0;
            else
            {
                double total_net = (double)collected_info[8] + network;
                collected_info[8] = total_net;
            }


            //getting information of threads       
            collected_info[9] = GetThreads(process);
            string[] all_threads = ((string)collected_info[9]).Split('|');
            //updating dict
            info[process.Id] = collected_info;
            PrintLine();
            PrintRow("pid", "process name", "cpu", "private memory", "private working set",
                "network", "total cpu", "total private memory", "total working set", "total network",
                "threads");
            PrintLine();
            PrintRow(process.Id.ToString(), collected_info[0].ToString(), collected_info[1].ToString(),
                collected_info[2].ToString(), collected_info[3].ToString(), collected_info[4].ToString(),
                collected_info[5].ToString(), collected_info[6].ToString(), collected_info[7].ToString(),
                collected_info[8].ToString(), all_threads[0].ToString());
            for (int i = 1; i < all_threads.Length; i++)
                PrintRow(" ", " ", " ", " ", " ", " ", " ", " ", " ", " ", all_threads[i].ToString());
            PrintLine();


        }
        public static void GetProcessors()
        {
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get())
            {
                Console.WriteLine("Number Of Physical Processors: {0} ", item["NumberOfProcessors"]);
            }
        }
        public static int GetCores()
        {
            int coreCount = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                coreCount += int.Parse(item["NumberOfCores"].ToString());
            }
            //Console.WriteLine("Number Of Cores: {0}", coreCount);
            return coreCount;
        }
        public static int GetLogical()
        {
            return Environment.ProcessorCount;
        }
        public static string GetThreads(Process process)
        {
            string data = "";
            ProcessThreadCollection threads = process.Threads;
            
            //get cpu of each thread
            foreach (ProcessThread thread in threads)
            {
                long currentTick = (DateTime.Now).Ticks;
                double currentcpu = thread.TotalProcessorTime.TotalMilliseconds;
                Thread.Sleep(500);
                long newTick = (DateTime.Now).Ticks;
                double newcpu = thread.TotalProcessorTime.TotalMilliseconds;
                double totalcpu = 100000 * (double)((newcpu - currentcpu)) / (double)(newTick - currentTick);

                if (thread.Id== Thread.CurrentThread.ManagedThreadId)
                    //data += thread.Id + ", " + thread.ThreadState + "," + totalcpu + "|";
                    Console.WriteLine($"tid: {thread.Id}, state: {thread.ThreadState}, cpu: {totalcpu}, core: {GetCurrentProcessorNumber()}");
                else
                    Console.WriteLine($"tid: {thread.Id}, state: {thread.ThreadState}, cpu: {totalcpu}");
            }
            return data;
        }

        public static void GetLogicalCpu()
        {
            int cores = GetLogical();
            for (int i=0; i<cores; i++)
            {
                PerformanceCounter pc = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                pc.NextValue();
                Thread.Sleep(10);
                Console.WriteLine("core {0}: {1}", i.ToString(), pc.NextValue());
            }
            
        }

        static void Main(string[] args)
        {
            Process[] processes = Process.GetProcesses();
            
            int pid = 548;
            Process process=Process.GetProcessById(pid);
            Console.WriteLine("would you like to recieve info about process or thread? (p/t):");
            char selection = char.Parse(Console.ReadLine());
            if (selection == 'p')
            {
                Console.WriteLine("enter process id: ");
                int process_id = int.Parse(Console.ReadLine());
                try
                {
                    Process selected_process = Process.GetProcessById(process_id);
                    Console.Clear();
                    //give process info
                }
                catch
                {
                    Console.WriteLine("process doesnt exist, try to run the program again");
                }
            }
            if (selection == 't')
            {
                Console.WriteLine("enter process id: ");
                int process_id = int.Parse(Console.ReadLine());
                try
                {
                    Process selected_process = Process.GetProcessById(process_id);
                    foreach (ProcessThread thread in selected_process.Threads)
                    {
                        Console.WriteLine($"Thread Id: {thread.Id}");
                    }
                    Console.WriteLine("select thread Id: ");
                    int thread_id = int.Parse(Console.ReadLine());
                    bool is_exist = false;
                    foreach (ProcessThread thread in selected_process.Threads)
                    {
                        if (thread_id == thread.Id)
                        {
                            is_exist = true;
                            Console.Clear();
                            //give thread info
                        }
                    }
                    if (!is_exist)
                        Console.WriteLine("thread doesnt exist");
                }
                catch
                {
                    Console.WriteLine("process doesnt exist, try to run the program again");
                }
            }
            //while (true)
            //{
            //    GetThreads(process);  
            //}
            //      Parallel.For(0, 1000000, state => Console.WriteLine("Thread Id = {0}, CoreId = {1}",
            //Thread.CurrentThread.ManagedThreadId,
            //GetCurrentProcessorNumber()));

            //GetLogicalCpu();
            Console.ReadKey();
            //while (true)
            //{
            //    OneProcess(info, process);
            //    Thread.Sleep(2000);
            //}
            Console.ReadKey();

        }
    }
}
