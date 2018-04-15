/* ***************************************************************************
 * This file is part of SharpNEAT - Evolution of Neural Networks.
 * 
 * Copyright 2004-2016 Colin Green (sharpneat@gmail.com)
 *
 * SharpNEAT is free software; you can redistribute it and/or modify
 * it under the terms of The MIT License (MIT).
 *
 * You should have received a copy of the MIT License
 * along with SharpNEAT; if not, see https://opensource.org/licenses/MIT.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SharpNeat.Core;
using SharpNeat.Phenomes;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using System.Xml;

namespace SharpNeat.Domains.EasyChange
{
    /// <summary>
    /// Binary 3-Multiplexer task.
    /// One binary input selects which of two other binary inputs to output. 
    /// </summary>
    public class EasyChangeEvaluator : IPhenomeEvaluator<IBlackBox>
    {
        ulong _evalCount;
        bool _stopConditionSatisfied;
        static List<double[]> moleculeCaracteristics;
        static int _totalImageCount;
        static int _pixelCount; // Todos las moléculas tienen la misma cantidad de características
        static double[] allIdentifiers;
        static List<double[]> allImages;
        static bool[] testClases;
        int separation;
        NeatEvolutionAlgorithm<NeatGenome> _ea;
        bool trainingMode;
        static int _maxGen;

        #region IPhenomeEvaluator<IBlackBox> Members

        public EasyChangeEvaluator(NeatEvolutionAlgorithm<NeatGenome> ea)
        {
            moleculeCaracteristics = DatasetDataLoader.loadDataset();
            //double[] debuj = GetRange(moleculeCaracteristics);
            _totalImageCount = moleculeCaracteristics.Count;
            _pixelCount = moleculeCaracteristics[0].Length - 1;
            allIdentifiers = new double[_totalImageCount];
            allImages = new List<double[]>();
            double temp = _totalImageCount * (1 - EasyChangeParams.TESTPORCENTAGE);
            separation = (int)temp;
            _ea = ea;
            trainingMode = true;
            _maxGen = EasyChangeParams.MAXGENERATIONS;
            _stopConditionSatisfied = false;

            // Mezclo los resultados
            Shuffle();      

            // Si se decide normalizar los datos
            if (EasyChangeParams.NORMALIZEDATA)
            {
                double[] secArray = new double[moleculeCaracteristics.Count];
                for (int i = 0; i < moleculeCaracteristics[0].Length - 1; i++) // No normalizar la salida
                {
                    for (int j = 0; j < moleculeCaracteristics.Count; j++)
                    {
                        secArray[j] = moleculeCaracteristics[j][i];
                    }
                    var normalizedArray = NormalizeData(secArray, -EasyChangeParams.NORMALIZERANGE, EasyChangeParams.NORMALIZERANGE);
                    for (int j = 0; j < moleculeCaracteristics.Count; j++)
                    {
                        moleculeCaracteristics[j][i] = normalizedArray[j];
                    }

                }
            }
            // Selecciono la salida
            for (int i = 0; i < moleculeCaracteristics.Count; i++)
            {
                // El valor de la propiedad a calcular se encuentra al final del arreglo
                allIdentifiers[i] = moleculeCaracteristics[i][_pixelCount];

                // Se evalúan todas las propiedades
                allImages.Add(moleculeCaracteristics[i].Take(_pixelCount).ToArray());
            }

            //Para analisis de clases

            testClases = new bool[allIdentifiers.Length];
            for (int p = 0; p < allIdentifiers.Length; p++)
                if (allIdentifiers[p] == 1.0)
                    testClases[p] = true;
                else
                    testClases[p] = false;

        }
        /// <summary>
        /// Gets the total number of evaluations that have been performed.
        /// </summary>
        public ulong EvaluationCount
        {
            get { return _evalCount; }
        }

        /// <summary>
        /// Gets a value indicating whether some goal fitness has been achieved and that
        /// the evolutionary algorithm/search should stop. This property's value can remain false
        /// to allow the algorithm to run indefinitely.
        /// </summary>
        public bool StopConditionSatisfied
        {
            get { return _stopConditionSatisfied; }
        }

        /// <summary>
        /// Evaluate the provided IBlackBox against the Binary 6-Multiplexer problem domain and return
        /// its fitness score.
        /// </summary>
        public FitnessInfo Evaluate(IBlackBox box)
        {
            double fitness = 0.0;
            double output;
            ISignalArray inputArr = box.InputSignalArray;
            ISignalArray outputArr = box.OutputSignalArray;
            _evalCount++;
     
            if(_ea.CurrentGeneration == _maxGen + 1)
            {
                trainingMode = false;
            }

            if (trainingMode) {
                
                for (int i = 0; i < separation; i++)
                {
                    for (int j = 0; j < _pixelCount; j++)
                    {
                        inputArr[j] = allImages[i][j];
                    }

                    // Activate the black box.
                    box.Activate();
                    if (!box.IsStateValid)
                    {   // Any black box that gets itself into an invalid state is unlikely to be
                        // any good, so let's just exit here.
                        return FitnessInfo.Zero;
                    }

                    // Read output signal.
                    output = outputArr[0];

                    if (output >= 0.5 && testClases[i] || output < 0.5 && !testClases[i])
                        fitness += 1.0;


                    // Reset black box state ready for next test case.
                    box.ResetState();
                }

                fitness /= separation;
                fitness *= 100;
                return new FitnessInfo(fitness, fitness);
            }
            else
            {
              if (_ea.CurrentGeneration == _maxGen + 2)
                {
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
              else
                {
                    for (int t = separation; t < _totalImageCount; t++)
                    {
                        for (int j = 0; j < _pixelCount; j++)
                        {
                            inputArr[j] = allImages[t][j];
                        }

                        // Activate the black box.
                        box.Activate();
                        if (!box.IsStateValid)
                        {   // Any black box that gets itself into an invalid state is unlikely to be
                            // any good, so let's just exit here.
                            return FitnessInfo.Zero;
                        }

                        // Read output signal.
                        output = outputArr[0];

                        if (output >= 0.5 && testClases[t] || output < 0.5 && !testClases[t])
                            fitness += 1.0;
                 

                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    fitness /= (_totalImageCount - separation);
                    fitness *= 100;
                    return new FitnessInfo(fitness, fitness);
                }
            }
        }

        /// <summary>
        /// Reset the internal state of the evaluation scheme if any exists.
        /// Note. The Binary Multiplexer problem domain has no internal state. This method does nothing.
        /// </summary>
        public void Reset()
        {
        }

        #endregion

        //Función secundaria de normalizado
        private static double[] NormalizeData(IEnumerable<double> data, int min, int max)
        {
            double dataMax = data.Max();
            double dataMin = data.Min();
            double range = dataMax - dataMin;

            return data
                .Select(d => (d - dataMin) / range)
                .Select(n => ((1 - n) * min + n * max))
                .ToArray();
        }

        // Devuelve un arreglo con el rango de cada caracteristica de las moleculas. Solo para propositos de debuj
        private double[] GetRange(List<double[]> valuesFROMcsv)
        {
            double[] range = new double[valuesFROMcsv[0].Length];
            double min = 0;
            double max = 0;
            for (int i = 0; i < valuesFROMcsv[0].Length; i++)
                for (int j = 0; j < valuesFROMcsv.Count; j++)
                {
                    if (j == 0)
                    {
                        min = valuesFROMcsv[j][i];
                        max = valuesFROMcsv[j][i];
                    }
                    else
                    {
                        if (valuesFROMcsv[j][i] < min)
                            min = valuesFROMcsv[j][i];
                        else if (valuesFROMcsv[j][i] > max)
                            max = valuesFROMcsv[j][i];
                    }
                    range[i] = max - min;
                }
            return range;
        }

        public static void Shuffle()
        {

            Random rng = new Random(EasyChangeParams.SEED);
            int n = moleculeCaracteristics.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = moleculeCaracteristics[k];
                moleculeCaracteristics[k] = moleculeCaracteristics[n];
                moleculeCaracteristics[n] = value;
            }
        }
    }
}
