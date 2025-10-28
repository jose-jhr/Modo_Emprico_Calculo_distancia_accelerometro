using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Numerics;

namespace SensorDataProcessor
{
    class Program
    {
        static void Main(string[] args)
        {
            // Ruta del archivo (al mismo nivel del ejecutable)
            string filePath = "test_2_20251027_113516_proc.txt";
            var data = LoadData(filePath);
            // Procesar datos según necesidad
            // Ejemplo: var filteredData = FilterPerChannelAndId(data);
        }

        // Cargar datos desde archivo
        static List<SensorData> LoadData(string filePath)
        {
            var data = new List<SensorData>();
            using (var reader = new StreamReader(filePath))
            {
                // Saltar las dos primeras líneas (headers)
                reader.ReadLine();
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(';');
                    var record = new SensorData
                    {
                        Time = double.Parse(values[0].Replace(".", ",")),
                        Timespan = double.Parse(values[1].Replace(".", ",")),
                        Canal = values[2],
                        Id = int.Parse(values[3]),
                        Sample = int.Parse(values[4]),
                        Gap = int.Parse(values[5]),
                        AccX = double.Parse(values[6].Replace(".", ",")),
                        AccY = double.Parse(values[7].Replace(".", ",")),
                        AccZ = double.Parse(values[8].Replace(".", ",")),
                        RawAccX = double.Parse(values[9].Replace(".", ",")),
                        RawAccY = double.Parse(values[10].Replace(".", ",")),
                        RawAccZ = double.Parse(values[11].Replace(".", ",")),
                        RawGirX = double.Parse(values[12].Replace(".", ",")),
                        RawGirY = double.Parse(values[13].Replace(".", ",")),
                        RawGirZ = double.Parse(values[14].Replace(".", ",")),
                        FiltAccX = double.Parse(values[15].Replace(".", ",")),
                        FiltAccY = double.Parse(values[16].Replace(".", ",")),
                        FiltAccZ = double.Parse(values[17].Replace(".", ",")),
                        FiltGirX = double.Parse(values[18].Replace(".", ",")),
                        FiltGirY = double.Parse(values[19].Replace(".", ",")),
                        FiltGirZ = double.Parse(values[20].Replace(".", ",")),
                        VelEje = double.Parse(values[21].Replace(".", ",")),
                        PosEje = double.Parse(values[22].Replace(".", ",")),
                        PosFilt = double.Parse(values[23].Replace(".", ","))
                    };
                    data.Add(record);
                }
            }
            return data;
        }

        // Filtro Butterworth pasa bajos (coeficientes)
        static List<(double[] b, double[] a)> ButterLpCoeffs(int order, double Wn)
        {
            var polesAnalog = new List<Complex>();
            for (int k = 1; k <= order; k++)
            {
                double theta = (2 * k - 1) * Math.PI / (2 * order);
                polesAnalog.Add(new Complex(-Math.Sin(theta), Math.Cos(theta)));
            }
            double Wa = 2 * Math.Tan(Math.PI * Wn / 2);
            var polesDigital = new List<Complex>();
            foreach (var p in polesAnalog)
                polesDigital.Add((1 + p / 2) / (1 - p / 2));
            var sos = new List<(double[] b, double[] a)>();
            for (int i = 0; i < order; i += 2)
            {
                if (i + 1 < order)
                {
                    var p1 = polesDigital[i];
                    var p2 = polesDigital[i + 1];
                    double a1 = -2 * p1.Real;
                    double a2 = p1.Magnitude * p1.Magnitude;
                    double gain = (1 + a1 + a2) / 4;
                    sos.Add((new double[] { gain, 2 * gain, gain }, new double[] { 1, a1, a2 }));
                }
                else
                {
                    var p = polesDigital[i].Real;
                    double gain = (1 + p) / 2;
                    sos.Add((new double[] { gain, gain }, new double[] { 1, -p }));
                }
            }
            return sos;
        }

        // Filtro Butterworth pasa altos (coeficientes)
        static List<(double[] b, double[] a)> ButterHpCoeffs(int order, double Wn)
        {
            var polesAnalog = new List<Complex>();
            for (int k = 1; k <= order; k++)
            {
                double theta = (2 * k - 1) * Math.PI / (2 * order);
                polesAnalog.Add(new Complex(-Math.Sin(theta), Math.Cos(theta)));
            }
            double Wa = 2 * Math.Tan(Math.PI * Wn / 2);
            var polesDigital = new List<Complex>();
            foreach (var p in polesAnalog)
                polesDigital.Add((1 + Wa / (2 * p)) / (1 - Wa / (2 * p)));
            var sos = new List<(double[] b, double[] a)>();
            for (int i = 0; i < order; i += 2)
            {
                if (i + 1 < order)
                {
                    var p1 = polesDigital[i];
                    double a1 = -2 * p1.Real;
                    double a2 = p1.Magnitude * p1.Magnitude;
                    double gain = (1 - a1 + a2) / 4;
                    sos.Add((new double[] { gain, -2 * gain, gain }, new double[] { 1, a1, a2 }));
                }
                else
                {
                    var p = polesDigital[i].Real;
                    double gain = (1 - p) / 2;
                    sos.Add((new double[] { gain, -gain }, new double[] { 1, -p }));
                }
            }
            return sos;
        }

        // Filtro Butterworth pasa banda (coeficientes)
        static List<(double[] b, double[] a)> ButterBpCoeffs(int order, double WnLow, double WnHigh)
        {
            var polesLp = new List<Complex>();
            for (int k = 1; k <= order; k++)
            {
                double theta = (2 * k - 1) * Math.PI / (2 * order);
                polesLp.Add(new Complex(-Math.Sin(theta), Math.Cos(theta)));
            }
            double w1 = 2 * Math.Tan(Math.PI * WnLow / 2);
            double w2 = 2 * Math.Tan(Math.PI * WnHigh / 2);
            double Bw = w2 - w1;
            double W0 = Math.Sqrt(w1 * w2);
            var polesBp = new List<Complex>();
            foreach (var p in polesLp)
            {
                double a = 1;
                double b = -p.Real * Bw;
                double c = W0 * W0;
                double discrim = b * b - 4 * a * c;
                double sqrtDisc = Math.Sqrt(Math.Abs(discrim));
                polesBp.Add((-b + sqrtDisc) / (2 * a));
                polesBp.Add((-b - sqrtDisc) / (2 * a));
            }
            var polesDigital = new List<Complex>();
            foreach (var p in polesBp)
                polesDigital.Add((2 + p) / (2 - p));
            var sos = new List<(double[] b, double[] a)>();
            for (int i = 0; i < polesDigital.Count; i += 2)
            {
                if (i + 1 < polesDigital.Count)
                {
                    var p1 = polesDigital[i];
                    var p2 = polesDigital[i + 1];
                    double a1 = -(p1.Real + p2.Real);
                    double a2 = (p1 * p2).Real;
                    double w = 2 * Math.PI * (WnLow + WnHigh) / 2;
                    double gain = 0.6;
                    sos.Add((new double[] { gain, 0, -gain }, new double[] { 1, a1, a2 }));
                }
            }
            return sos;
        }

        // Aplicar filtro SOS (filtfilt)
        static double[] ApplySosFiltfilt(double[] data, List<(double[] b, double[] a)> sos)
        {
            double[] y = (double[])data.Clone();
            foreach (var (b, a) in sos)
                y = BiquadDf2t(y, b, a);
            Array.Reverse(y);
            foreach (var (b, a) in sos)
                y = BiquadDf2t(y, b, a);
            Array.Reverse(y);
            return y;
        }

        // Filtro biquad directo
        static double[] BiquadDf2t(double[] x, double[] b, double[] a)
        {
            double[] y = new double[x.Length];
            if (b.Length == 2 && a.Length == 2)
            {
                double s = 0.0;
                for (int i = 0; i < x.Length; i++)
                {
                    y[i] = b[0] * x[i] + s;
                    s = b[1] * x[i] - a[1] * y[i];
                }
            }
            else
            {
                double s1 = 0.0, s2 = 0.0;
                for (int i = 0; i < x.Length; i++)
                {
                    y[i] = b[0] * x[i] + s1;
                    s1 = b[1] * x[i] - a[1] * y[i] + s2;
                    s2 = b[2] * x[i] - a[2] * y[i];
                }
            }
            return y;
        }

        // Filtro pasa bajos
        static double[] LowpassFilter(double[] data, double cutoff, double fs, int order = 4)
        {
            double nyq = 0.5 * fs;
            double Wn = cutoff / nyq;
            if (!(0 < Wn && Wn < 1))
                throw new ArgumentException("La frecuencia de corte debe estar entre 0 y Nyquist");
            var sos = ButterLpCoeffs(order, Wn);
            return ApplySosFiltfilt(data, sos);
        }

        // Filtro pasa altos
        static double[] HighpassFilter(double[] data, double cutoff, double fs, int order = 4)
        {
            double nyq = 0.5 * fs;
            double Wn = cutoff / nyq;
            if (!(0 < Wn && Wn < 1))
                throw new ArgumentException("La frecuencia de corte debe estar entre 0 y Nyquist");
            var sos = ButterHpCoeffs(order, Wn);
            return ApplySosFiltfilt(data, sos);
        }

        // Filtro pasa banda
        static double[] BandpassFilter(double[] data, double lowcut, double highcut, double fs, int order = 4)
        {
            double nyq = 0.5 * fs;
            double low = lowcut / nyq;
            double high = highcut / nyq;
            if (!(0 < low && low < high && high < 1))
                throw new ArgumentException("Las frecuencias deben estar entre 0 y Nyquist y lowcut < highcut");
            var sos = ButterBpCoeffs(order, low, high);
            return ApplySosFiltfilt(data, sos);
        }

        // Eliminar gravedad
        static List<SensorData> EliminarGravedad(List<SensorData> data, double alpha = 0.9)
        {
            double[] accX = data.Select(d => d.FiltAccX).ToArray();
            double[] accY = data.Select(d => d.FiltAccY).ToArray();
            double[] accZ = data.Select(d => d.FiltAccZ).ToArray();
            double[] gravityX = new double[accX.Length];
            double[] gravityY = new double[accY.Length];
            double[] gravityZ = new double[accZ.Length];
            double[] linAccX = new double[accX.Length];
            double[] linAccY = new double[accY.Length];
            double[] linAccZ = new double[accZ.Length];
            gravityX[0] = accX[0];
            gravityY[0] = accY[0];
            gravityZ[0] = accZ[0];
            for (int i = 1; i < accX.Length; i++)
            {
                gravityX[i] = alpha * gravityX[i - 1] + (1 - alpha) * accX[i];
                gravityY[i] = alpha * gravityY[i - 1] + (1 - alpha) * accY[i];
                gravityZ[i] = alpha * gravityZ[i - 1] + (1 - alpha) * accZ[i];
                linAccX[i] = accX[i] - gravityX[i];
                linAccY[i] = accY[i] - gravityY[i];
                linAccZ[i] = accZ[i] - gravityZ[i];
            }
            for (int i = 0; i < data.Count; i++)
            {
                data[i].LinAccXRaw = linAccX[i];
                data[i].LinAccYRaw = linAccY[i];
                data[i].LinAccZRaw = linAccZ[i];
            }
            return data;
        }

        // Filtrar por canal e ID
        static List<SensorData> FilterPerChannelAndId(List<SensorData> data, double low = 0.3, double high = 3, double fs = 50)
        {
            var filteredData = new List<SensorData>();
            var channels = data.Select(d => d.Canal).Distinct();
            foreach (var canal in channels)
            {
                var canalData = data.Where(d => d.Canal == canal).ToList();
                var ids = canalData.Select(d => d.Id).Distinct();
                foreach (var id in ids)
                {
                    var subData = canalData.Where(d => d.Id == id).ToList();
                    if (subData.Count == 0) continue;
                    var accX = subData.Select(d => d.LinAccXRaw).ToArray();
                    var accY = subData.Select(d => d.LinAccYRaw).ToArray();
                    var accZ = subData.Select(d => d.LinAccZRaw).ToArray();
                    accX = BandpassFilter(accX, low, high, fs);
                    accY = BandpassFilter(accY, low, high, fs);
                    accZ = BandpassFilter(accZ, low, high, fs);
                    for (int i = 0; i < subData.Count; i++)
                    {
                        subData[i].LinAccXRaw = accX[i];
                        subData[i].LinAccYRaw = accY[i];
                        subData[i].LinAccZRaw = accZ[i];
                    }
                    filteredData.AddRange(subData);
                }
            }
            return filteredData;
        }
    }

    // Clase para almacenar los datos de cada registro
    class SensorData
    {
        public double Time { get; set; }
        public double Timespan { get; set; }
        public string Canal { get; set; }
        public int Id { get; set; }
        public int Sample { get; set; }
        public int Gap { get; set; }
        public double AccX { get; set; }
        public double AccY { get; set; }
        public double AccZ { get; set; }
        public double RawAccX { get; set; }
        public double RawAccY { get; set; }
        public double RawAccZ { get; set; }
        public double RawGirX { get; set; }
        public double RawGirY { get; set; }
        public double RawGirZ { get; set; }
        public double FiltAccX { get; set; }
        public double FiltAccY { get; set; }
        public double FiltAccZ { get; set; }
        public double FiltGirX { get; set; }
        public double FiltGirY { get; set; }
        public double FiltGirZ { get; set; }
        public double VelEje { get; set; }
        public double PosEje { get; set; }
        public double PosFilt { get; set; }
        public double LinAccXRaw { get; set; }
        public double LinAccYRaw { get; set; }
        public double LinAccZRaw { get; set; }
    }
}
