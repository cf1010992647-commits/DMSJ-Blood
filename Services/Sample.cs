using System.Collections.Generic;

namespace Blood_Alcohol.Services
{
    public class Sample
    {
        // DMSJ：样本编号默认空串，避免空引用告警。
        public string BloodCode { get; set; } = string.Empty;

        public string Headspace1 => BloodCode + "A";

        public string Headspace2 => BloodCode + "B";

        public double Weight1 { get; set; }

        public double Weight2 { get; set; }

        public double BloodWeight { get; set; }
    }

    public class SampleManager
    {
        private readonly Queue<Sample> _samples = new();

        public Sample? Current { get; private set; }

        public void AddSample(string code)
        {
            _samples.Enqueue(new Sample
            {
                BloodCode = code
            });
        }

        public void Next()
        {
            if (_samples.Count > 0)
                Current = _samples.Dequeue();
        }
    }
}
