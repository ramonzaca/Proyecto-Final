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

        NeatEvolutionAlgorithm<NeatGenome> _ea;
        bool trainingMode;
        static int _maxGen;
        static List<double[]> moleculeCaracteristics;
        static int _totalImageCount;
        static int _pixelCount; // Todos las moléculas tienen la misma cantidad de características
        static double[] allIdentifiers;
        static List<double[]> allImages;
        static bool[] testClases;
        static int separation;

        #region IPhenomeEvaluator<IBlackBox> Members

        public EasyChangeEvaluator(NeatEvolutionAlgorithm<NeatGenome> ea, EasyChangeDataLoader dataLoader, int maxGen, double testPorcentage)
        {
            
            _ea = ea;
            trainingMode = true;
            _maxGen = maxGen;
            _stopConditionSatisfied = false;
            moleculeCaracteristics = dataLoader.MoleculeCaracteristics;
            _totalImageCount = dataLoader.TotalImageCount;
            _pixelCount = dataLoader.PixelCount;
            allIdentifiers = dataLoader.AllIdentifiers;
            allImages = dataLoader.AllImages;
            testClases = dataLoader.TestClases;
            double temp = _totalImageCount * (1 - testPorcentage);
            separation = (int)temp;

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

        
    }
}
