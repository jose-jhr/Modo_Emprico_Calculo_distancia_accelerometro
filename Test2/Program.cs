using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Globalization;
using System.IO;

namespace SensorDataProcessor
{
    class Program
    {
        // Factor pico para el cálculo del umbral mínimo de detección de picos
        const double FACTOR_PICO = 0.3;
        // Número de muestras mínimo entre picos detectados
        const int FACTOR_DISTANCIA = 35;

        static void Main(string[] args)
        {
            string filePath = "test_2_20251027_114217_proc.txt";
            var data = LoadData(filePath);
            data = data.Where(d => d.Id == 1).ToList();

            // PASO 1: Copiar valores filtrados (NO eliminar gravedad)
            foreach (var d in data)
            {
                d.LinAccXRaw = d.FiltAccX;
                d.LinAccYRaw = d.FiltAccY;
                d.LinAccZRaw = d.FiltAccZ;
            }

            // PASO 2: Solo ordenar (NO aplicar filtro pasa banda)
            var dfFiltrado = data.OrderBy(d => d.Canal)
                                 .ThenBy(d => d.Id)
                                 .ThenBy(d => d.Timespan)
                                 .ToList();

            // PASO 3: Filtrar por canal
            var df2 = dfFiltrado.Where(d => d.Canal == "Mov2").ToList();
            var df3 = dfFiltrado.Where(d => d.Canal == "Mov").ToList();
            var df4 = dfFiltrado.Where(d => d.Canal == "Wrist").ToList();

            // Configuración de bandas y parámetros
            var bandas = new List<(double, double, int)>
            {
                (0.0, 4.82, 4),
                (4.82, 8.7, 2),
                (8.7, double.PositiveInfinity, 1)
            };

            var parametrosPorMetodo = new Dictionary<string, Dictionary<string, object>>
            {
                ["weinberg"] = new Dictionary<string, object>
                {
                    ["K"] = 0.315,
                    ["K_height"] = 0.000000005,
                    ["distancia_picos"] = FACTOR_DISTANCIA,
                    ["dt"] = 0.02,
                    ["alpha"] = 0.1,
                    ["beta"] = 0.4,
                    ["use_fft"] = false
                },
                ["adaptive"] = new Dictionary<string, object>
                {
                    ["K"] = 0.22,
                    ["K_height"] = 0.000000005,
                    ["distancia_picos"] = FACTOR_DISTANCIA,
                    ["dt"] = 0.02,
                    ["alpha"] = 0.57,
                    ["beta"] = 0.17,
                    ["use_fft"] = true
                }
            };

            var metodos = new[] { "weinberg", "adaptive" };
            var sensores = new[]
            {
                ("Tobillo D", df2),
                ("Tobillo I", df3),
                ("Sensor Dorsal", df4)
            };

            foreach (var method in metodos)
            {
                Console.WriteLine();
                Console.WriteLine(new string('=', 120));
                Console.WriteLine($"RESULTADOS PARA METODO: {method.ToUpper()} 4 metros");
                Console.WriteLine(new string('=', 120));
                double distanciaAcumulada = 0.0;

                foreach (var (nombre, dfSensor) in sensores)
                {
                    var dfSensorCopy = CopiarDataset(dfSensor);
                    Console.WriteLine("Sensor usado: " + nombre);
                    var parametros = parametrosPorMetodo[method];
                    var result = CalcularTiempoYScore3(
                        dfSensorCopy,
                        2.0,
                        "X",
                        bandas,
                        method,
                        (double)parametros["K"],
                        (double)parametros["K_height"],
                        (int)parametros["distancia_picos"],
                        (double)parametros["dt"],
                        (double)parametros["alpha"],
                        (double)parametros["beta"],
                        (bool)parametros["use_fft"]
                    );

                    double distanciaRecorrida = result.DistanciaRecorrida;
                    if (nombre != "Sensor Dorsal")
                        distanciaAcumulada += distanciaRecorrida;

                    // Imprimir resultado del sensor
                    Console.WriteLine();
                    Console.WriteLine($"Sensor: {nombre}");
                    Console.WriteLine($"  Distancia recorrida (m): {distanciaRecorrida:F2}");
                    Console.WriteLine($"  Tiempo (s): {result.Tiempo:F2}");
                    Console.WriteLine($"  Puntuacion (0-4): {result.Puntuacion04}");
                    Console.WriteLine($"  Puntuacion custom: {result.PuntuacionCustom:F2}");
                    Console.WriteLine($"  Completado: {(result.Completado ? "x" : "-")}");

                    if (result.Pasos > 0)
                    {
                        Console.WriteLine($"  Frecuencia (Hz): {result.Frecuencia:F2}");
                        Console.WriteLine($"  Cadencia (ppm): {(int)Math.Round(result.Cadencia)}");
                        Console.WriteLine($"  Pasos: {result.Pasos}");
                        Console.WriteLine($"  Tiempo medio paso (s): {result.TiempoMedioPaso:F2}");
                        Console.WriteLine($"  Altura pasos (cm): [{string.Join(", ", result.AlturaPasos.Select(x => x.ToString("F2")))}]");
                        Console.WriteLine($"  Array Rom x Pasos (metros): [{string.Join(", ", result.ArrayRomPasos.Select(x => x.ToString("F2")))}]");
                    }
                    else
                    {
                        Console.WriteLine($"  Frecuencia (Hz): -");
                        Console.WriteLine($"  Cadencia (ppm): -");
                        Console.WriteLine($"  Pasos: -");
                        Console.WriteLine($"  Tiempo medio paso (s): -");
                    }

                    if (nombre != "Sensor Dorsal")
                        Console.WriteLine($"  Distancia Acumulada: {distanciaAcumulada:F2}");
                    else
                        Console.WriteLine($"  Distancia Acumulada: NaN");
                }
            }
        }

        static List<SensorData> CopiarDataset(List<SensorData> original)
        {
            return original.Select(d => new SensorData
            {
                Time = d.Time,
                Timespan = d.Timespan,
                Canal = d.Canal,
                Id = d.Id,
                Sample = d.Sample,
                Gap = d.Gap,
                AccX = d.AccX,
                AccY = d.AccY,
                AccZ = d.AccZ,
                RawAccX = d.RawAccX,
                RawAccY = d.RawAccY,
                RawAccZ = d.RawAccZ,
                RawGirX = d.RawGirX,
                RawGirY = d.RawGirY,
                RawGirZ = d.RawGirZ,
                FiltAccX = d.FiltAccX,
                FiltAccY = d.FiltAccY,
                FiltAccZ = d.FiltAccZ,
                FiltGirX = d.FiltGirX,
                FiltGirY = d.FiltGirY,
                FiltGirZ = d.FiltGirZ,
                VelEje = d.VelEje,
                PosEje = d.PosEje,
                PosFilt = d.PosFilt,
                LinAccXRaw = d.LinAccXRaw,
                LinAccYRaw = d.LinAccYRaw,
                LinAccZRaw = d.LinAccZRaw
            }).ToList();
        }

        static List<SensorData> LoadData(string filePath)
        {
            var data = new List<SensorData>();
            using (var reader = new StreamReader(filePath))
            {
                reader.ReadLine();
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var values = line.Split(';');
                    if (values.Length < 24) continue;
                    data.Add(new SensorData
                    {
                        Time = values[0],
                        Timespan = ParseDouble(values[1]),
                        Canal = values[2],
                        Id = int.Parse(values[3]),
                        Sample = int.Parse(values[4]),
                        Gap = int.Parse(values[5]),
                        AccX = ParseDouble(values[6]),
                        AccY = ParseDouble(values[7]),
                        AccZ = ParseDouble(values[8]),
                        RawAccX = ParseDouble(values[9]),
                        RawAccY = ParseDouble(values[10]),
                        RawAccZ = ParseDouble(values[11]),
                        RawGirX = ParseDouble(values[12]),
                        RawGirY = ParseDouble(values[13]),
                        RawGirZ = ParseDouble(values[14]),
                        FiltAccX = ParseDouble(values[15]),
                        FiltAccY = ParseDouble(values[16]),
                        FiltAccZ = ParseDouble(values[17]),
                        FiltGirX = ParseDouble(values[18]),
                        FiltGirY = ParseDouble(values[19]),
                        FiltGirZ = ParseDouble(values[20]),
                        VelEje = ParseDouble(values[21]),
                        PosEje = ParseDouble(values[22]),
                        PosFilt = ParseDouble(values[23])
                    });
                }
            }
            return data;
        }

        static double ParseDouble(string value)
        {
            return double.Parse(value.Replace(",", "."), CultureInfo.InvariantCulture);
        }

        static List<int> FindPeaksScipyStyle(double[] x, double? height = null, int? distance = null)
        {
            var peaks = new List<int>();
            int n = x.Length;
            for (int i = 1; i < n - 1; i++)
            {
                if (x[i] > x[i - 1] && x[i] > x[i + 1])
                    peaks.Add(i);
            }
            if (height.HasValue)
                peaks = peaks.Where(i => x[i] >= height.Value).ToList();
            if (distance.HasValue && peaks.Count > 1)
            {
                var filteredPeaks = new List<int>();
                int lastPeak = -distance.Value - 1;
                foreach (var peak in peaks)
                {
                    if (peak - lastPeak >= distance.Value)
                    {
                        filteredPeaks.Add(peak);
                        lastPeak = peak;
                    }
                    else if (x[peak] > x[filteredPeaks.Last()])
                    {
                        filteredPeaks[filteredPeaks.Count - 1] = peak;
                        lastPeak = peak;
                    }
                }
                peaks = filteredPeaks;
            }
            return peaks;
        }

        static double WeinbergStepLength(double accMax, double accMin, double K = 0.5)
        {
            return K * Math.Pow(accMax - accMin, 0.25);
        }

        static double KimStepLength(double accMax, double accMin, double K = 0.4)
        {
            return K * Math.Sqrt(accMax - accMin);
        }

        static double ScarletStepLength(double[] accValues, double K = 0.45)
        {
            return K * accValues.Average(a => Math.Abs(a));
        }

        static double CalculateStepHeight(double accMax, double accMin, double stepTime, double? KHeight = null)
        {
            double accAmplitude = accMax - accMin;

            double kHeightValue;
            if (KHeight == null)
            {
                // Factor ajustado para obtener altura en cm
                kHeightValue = 100 * (0.00001 + (accAmplitude / 100.0) * 0.001);
            }
            else
            {
                kHeightValue = KHeight.Value;
            }

            // Altura en cm
            double height = kHeightValue * accAmplitude * Math.Pow(stepTime, 2);
            return height;
        }


        static double CalculateStepVelocity(List<SensorData> df, int peakIdx, int valleyIdx, double dt = 0.02)
        {
            int start = Math.Max(0, valleyIdx - 10);
            int end = Math.Min(df.Count - 1, peakIdx + 10);
            double[] accSegment = df.Skip(start).Take(end - start).Select(d => d.LinAccYRaw).ToArray();
            double[] velocity = new double[accSegment.Length];
            for (int i = 1; i < accSegment.Length; i++)
                velocity[i] = velocity[i - 1] + accSegment[i] * dt;
            return velocity.Average(v => Math.Abs(v));
        }

        static double AdaptiveStepLength(double accMax, double accMin, double velocity, double alpha = 0.1, double beta = 0.4)
        {
            double K = alpha * velocity + beta;
            return K * Math.Pow(accMax - accMin, 0.25);
        }

        static double EstimateStepHeightFromLength(double stepLength, double height)
        {
            double legLength = 0.53 * height;
            double ratio = stepLength / (2.0 * legLength);
            ratio = Math.Min(1.0, ratio);
            return legLength * (1.0 - Math.Sqrt(1.0 - ratio * ratio));
        }

        static double[] SmoothWithFft(double[] signal, double threshold = 0.1)
        {
            int n = signal.Length;
            Complex[] fftSignal = new Complex[n];
            for (int i = 0; i < n; i++)
                fftSignal[i] = new Complex(signal[i], 0);
            FFT(fftSignal, false);
            for (int i = 0; i < n; i++)
            {
                double freq = (i <= n / 2) ? (double)i / n : (double)(i - n) / n;
                if (Math.Abs(freq) > threshold)
                    fftSignal[i] = Complex.Zero;
            }
            FFT(fftSignal, true);
            double[] smoothed = new double[n];
            for (int i = 0; i < n; i++)
                smoothed[i] = fftSignal[i].Real;
            return smoothed;
        }

        static void FFT(Complex[] data, bool inverse)
        {
            int n = data.Length;
            if (n <= 1) return;
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    var temp = data[i];
                    data[i] = data[j];
                    data[j] = temp;
                }
                int k = n / 2;
                while (k <= j)
                {
                    j -= k;
                    k /= 2;
                }
                j += k;
            }
            for (int len = 2; len <= n; len *= 2)
            {
                double angle = (inverse ? 2 : -2) * Math.PI / len;
                Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int j2 = 0; j2 < len / 2; j2++)
                    {
                        Complex u = data[i + j2];
                        Complex v = data[i + j2 + len / 2] * w;
                        data[i + j2] = u + v;
                        data[i + j2 + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
            if (inverse)
            {
                for (int i = 0; i < n; i++)
                    data[i] /= n;
            }
        }

        static ResultadoCalculo CalcularTiempoYScore3(
            List<SensorData> dfFiltrado,
            double distanciaObjetivo = 2,
            string eje = "X",
            List<(double, double, int)> bandas = null,
            string method = "adaptive",
            double K = 0.22,
            double KHeight = 0.0003,
            int distanciaPicos = FACTOR_DISTANCIA,
            double dt = 0.02,
            double alpha = 0.1,
            double beta = 0.23,
            bool useFft = true)
        {
            if (dfFiltrado.Count == 0)
                return new ResultadoCalculo
                {
                    DistanciaRecorrida = 0.0,
                    Tiempo = 0.0,
                    Puntuacion04 = 0,
                    PuntuacionCustom = 0.0,
                    Completado = false,
                    AlturaPasos = new double[0],
                    DuracionEjercicio = 0.0,
                    ArrayRomPasos = new List<double>(),
                    Pasos = 0,
                    Frecuencia = 0.0,
                    Cadencia = 0.0,
                    TiempoMedioPaso = 0.0
                };

            // Usar LinAccXRaw
            double[] signal = dfFiltrado.Select(d => d.LinAccXRaw).ToArray();
            int originalLength = signal.Length;

            // Aplicar FFT si está habilitado
            if (useFft)
            {
                int nextPow2 = (int)Math.Pow(2, Math.Ceiling(Math.Log(signal.Length, 2)));
                Array.Resize(ref signal, nextPow2);
                signal = SmoothWithFft(signal, 0.1);
                Array.Resize(ref signal, originalLength);
            }

            // Detectar picos
            double alturaMinima = FACTOR_PICO * signal.Max();
            var peaks = FindPeaksScipyStyle(signal, alturaMinima, distanciaPicos);
            if (peaks.Count == 0)
                return new ResultadoCalculo
                {
                    DistanciaRecorrida = 0.0,
                    Tiempo = 0.0,
                    Puntuacion04 = 0,
                    PuntuacionCustom = 0.0,
                    Completado = false,
                    AlturaPasos = new double[0],
                    DuracionEjercicio = 0.0,
                    ArrayRomPasos = new List<double>(),
                    Pasos = 0,
                    Frecuencia = 0.0,
                    Cadencia = 0.0,
                    TiempoMedioPaso = 0.0
                };

            // Procesar cada paso
            var stepData = new List<StepInfo>();
            foreach (var p in peaks)
            {
                int start = Math.Max(0, p - 20);
                while (start > 1 && signal[start] > signal[start - 1])
                    start--;
                int end = Math.Min(signal.Length - 1, p + 20);
                while (end < signal.Length - 2 && signal[end] > signal[end + 1])
                    end++;
                double accMax = signal[p];
                double accMin = Math.Min(signal[start], signal[end]);
                double[] accValues = signal.Skip(start).Take(end - start + 1).ToArray();
                double stepTime = (end - start) * dt;

                //double stepTime = dfFiltrado[end].Timespan - dfFiltrado[start].Timespan;
                double stepHeight = CalculateStepHeight(accMax, accMin, stepTime, null);
                stepData.Add(new StepInfo
                {
                    AccMax = accMax,
                    AccMin = accMin,
                    AccValues = accValues,
                    StepTime = stepTime,
                    StepHeight = stepHeight,
                    EndIdx = end
                });
            }

            // Ajuste con búsqueda binaria para K
            double KMin = K * 0.1;
            double KMax = K * 5.0;
            double KOptimo = K;
            double alphaOptimo = alpha;
            double betaOptimo = beta;

            for (int iteracion = 0; iteracion < 340; iteracion++)
            {
                double KMid = (KMin + KMax) / 2.0;
                double alphaMid = alphaOptimo;
                double betaMid = betaOptimo;

                if (method == "adaptive")
                {
                    double ratio = KMid / K;
                    alphaMid = alpha * ratio;
                    betaMid = beta * ratio;
                }

                double totalDistance = 0.0;
                foreach (var step in stepData)
                {
                    double stepLength;
                    switch (method)
                    {
                        case "weinberg":
                            stepLength = WeinbergStepLength(step.AccMax, step.AccMin, KMid);
                            break;
                        case "kim":
                            stepLength = KimStepLength(step.AccMax, step.AccMin, KMid);
                            break;
                        case "scarlet":
                            stepLength = ScarletStepLength(step.AccValues, KMid);
                            break;
                        case "adaptive":
                            double velocity = CalculateStepVelocity(dfFiltrado, step.EndIdx, step.EndIdx - 10, dt);
                            stepLength = AdaptiveStepLength(step.AccMax, step.AccMin, velocity, alphaMid, betaMid);
                            break;
                        default:
                            stepLength = WeinbergStepLength(step.AccMax, step.AccMin, KMid);
                            break;
                    }
                    totalDistance += stepLength;
                }

                if (Math.Abs(totalDistance - distanciaObjetivo) < 0.04)
                {
                    KOptimo = KMid;
                    alphaOptimo = alphaMid;
                    betaOptimo = betaMid;
                    break;
                }

                if (totalDistance < distanciaObjetivo)
                    KMin = KMid;
                else
                    KMax = KMid;

                KOptimo = KMid;
                alphaOptimo = alphaMid;
                betaOptimo = betaMid;
            }

            // Calcular resultados finales con K óptimo
            double totalDistanceFinal = 0.0;
            var stepHeights = new List<double>();
            var arrayRomPasos = new List<double>();
            foreach (var step in stepData)
            {
                double stepLength;
                switch (method)
                {
                    case "weinberg":
                        stepLength = WeinbergStepLength(step.AccMax, step.AccMin, KOptimo);
                        break;
                    case "kim":
                        stepLength = KimStepLength(step.AccMax, step.AccMin, KOptimo);
                        break;
                    case "scarlet":
                        stepLength = ScarletStepLength(step.AccValues, KOptimo);
                        break;
                    case "adaptive":
                        double velocity = CalculateStepVelocity(dfFiltrado, step.EndIdx, step.EndIdx - 10, dt);
                        stepLength = AdaptiveStepLength(step.AccMax, step.AccMin, velocity, alphaOptimo, betaOptimo);
                        break;
                    default:
                        stepLength = WeinbergStepLength(step.AccMax, step.AccMin, KOptimo);
                        break;
                }
                totalDistanceFinal += stepLength;
                arrayRomPasos.Add(stepLength);
                stepHeights.Add(step.StepHeight);
            }

            // Métricas temporales
            int pasos = peaks.Count;
            double tiempoTotal = pasos > 0 ? stepData.Last().EndIdx * dt : 0.0;
            double frecuenciaHz = tiempoTotal > 0 ? pasos / tiempoTotal : 0.0;
            double cadenciaPpm = frecuenciaHz * 60 * 2;
            double tiempoMedioPaso = pasos > 0 ? (tiempoTotal / pasos) / 2 : 0.0;

            // Puntuación
            double exerciseDuration = stepData.Sum(s => s.StepTime);
            bool completado = totalDistanceFinal >= distanciaObjetivo;
            int score04 = 0;
            double scoreCustom = 0.0;
            if (bandas != null && completado)
            {
                double tiempoUsado = exerciseDuration;
                foreach (var (low, high, puntos) in bandas)
                {
                    if (low <= tiempoUsado && tiempoUsado <= high)
                    {
                        score04 = puntos;
                        break;
                    }
                }
                scoreCustom = ScoreCustomGait(tiempoUsado, bandas);
            }

            double[] stepHeightsArray = stepHeights.Select(h => Math.Round(h * 100, 2)).ToArray();
            return new ResultadoCalculo
            {
                DistanciaRecorrida = Math.Round(totalDistanceFinal, 2),
                Tiempo = Math.Round(tiempoTotal, 2),
                Puntuacion04 = score04,
                PuntuacionCustom = Math.Round(scoreCustom, 3),
                Completado = completado,
                AlturaPasos = stepHeightsArray,
                DuracionEjercicio = Math.Round(exerciseDuration, 2),
                ArrayRomPasos = arrayRomPasos,
                Pasos = pasos,
                Frecuencia = Math.Round(frecuenciaHz, 3),
                Cadencia = Math.Round(cadenciaPpm, 1),
                TiempoMedioPaso = Math.Round(tiempoMedioPaso, 3)
            };
        }

        static double ScoreCustomGait(double timeS, List<(double, double, int)> bandas)
        {
            if (bandas == null || !double.IsFinite(timeS))
                return 0.0;
            bandas = bandas.OrderBy(b => b.Item2).ToList();
            for (int i = 0; i < bandas.Count; i++)
            {
                var (low, high, pHi) = bandas[i];
                if (timeS <= high)
                {
                    if (i == bandas.Count - 1 || !double.IsFinite(high))
                        return Math.Clamp(pHi, 0.0, 4.0);
                    var (_, _, pNext) = bandas[i + 1];
                    double denom = Math.Max(high - low, 1e-9);
                    double alpha2 = (high - timeS) / denom;
                    double s = pNext + (pHi - pNext) * Math.Clamp(alpha2, 0.0, 1.0);
                    return Math.Clamp(s, 0.0, 4.0);
                }
            }
            return Math.Clamp(bandas.Last().Item3, 0.0, 4.0);
        }
    }

    class StepInfo
    {
        public double AccMax { get; set; }
        public double AccMin { get; set; }
        public double[] AccValues { get; set; }
        public double StepTime { get; set; }
        public double StepHeight { get; set; }
        public int EndIdx { get; set; }
    }

    class ResultadoCalculo
    {
        public double DistanciaRecorrida { get; set; }
        public double Tiempo { get; set; }
        public int Puntuacion04 { get; set; }
        public double PuntuacionCustom { get; set; }
        public bool Completado { get; set; }
        public double[] AlturaPasos { get; set; }
        public double DuracionEjercicio { get; set; }
        public List<double> ArrayRomPasos { get; set; }
        public int Pasos { get; set; }
        public double Frecuencia { get; set; }
        public double Cadencia { get; set; }
        public double TiempoMedioPaso { get; set; }
    }

    class SensorData
    {
        public string Time { get; set; }
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
