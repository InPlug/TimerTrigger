using System;
using System.IO;
using Vishnu.Interchange;

namespace TimerTriggerDemo
{
    class Program
    {
        public static void Main(string[] args)
        {
            TimerTrigger.TimerTrigger trigger = new TimerTrigger.TimerTrigger();
            trigger.Start(null, @"S:5|S:3", trigger_TriggerIt);
            Console.WriteLine("stop trigger mit enter");
            Console.ReadLine();
            trigger.Stop(null, trigger_TriggerIt);
            Console.WriteLine("Trigger stopped");
            Console.ReadLine();
        }

        static void trigger_TriggerIt(TreeEvent source)
        {
            Console.WriteLine(String.Format($"Trigger feuert ({source.SourceId}{source.SenderId})."));
            try
            {
                File.SetLastWriteTimeUtc(@"c:\Logs\Vishnu\CheckAll\JobSnapshot.xml", DateTime.UtcNow);
            }
            catch { }
        }
    }
}
