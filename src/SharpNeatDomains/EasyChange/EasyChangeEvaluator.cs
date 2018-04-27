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
        bool _trainingMode;
        static int _maxGen;
        static List<double[]> _moleculeCaracteristics;
        static int _moleculesCount;
        static int _caracteristicsCount; 
        static List<double[]> _moleculesData;
        static bool[] _moleculesResults;
        static int _separation;
        static int _fitnessFunction;

        #region IPhenomeEvaluator<IBlackBox> Members

        public EasyChangeEvaluator(NeatEvolutionAlgorithm<NeatGenome> ea, EasyChangeDataLoader dataLoader, int maxGen, double testPorcentage, int fitnessFunction)
        {
            
            _ea = ea;
            _trainingMode = true;
            _maxGen = maxGen;
            _stopConditionSatisfied = false;
            _moleculeCaracteristics = dataLoader.MoleculeCaracteristics;
            _moleculesCount = dataLoader.MoleculesCount;
            _caracteristicsCount = dataLoader.CaracteristicsCount;
            _moleculesData = dataLoader.MoleculesData;
            _moleculesResults = dataLoader.MoleculesResults;
            double temp = _moleculesCount * (1 - testPorcentage);
            _separation = (int)temp;
            _fitnessFunction = fitnessFunction;

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
        /// Evaluate the provided IBlackBox against the choosen fitness function and return
        /// its fitness score.
        /// </summary>
        public FitnessInfo Evaluate(IBlackBox box)
        {
            if (_fitnessFunction == 0)
                return Accuracy(box);
            else
                return EscalatedAccuracy(box);
            
        }

        /// <summary>
        /// Reset the internal state of the evaluation scheme if any exists.
        /// Note. The classification problem domain has no internal state. This method does nothing.
        /// </summary>
        public void Reset()
        {
        }

        #endregion

        /// <summary>
        /// Fitness function case: Accuracy
        /// </summary>
        private FitnessInfo Accuracy(IBlackBox box)
        {
            double fitness = 0.0;
            double output;
            ISignalArray inputArr = box.InputSignalArray;
            ISignalArray outputArr = box.OutputSignalArray;
            _evalCount++;

            // If it should analize the training or the test data.
            if (_ea.CurrentGeneration == _maxGen + 1)
            {
                _trainingMode = false;
            }

            // Training mode case
            if (_trainingMode)
            {
                for (int i = 0; i < _separation; i++)
                {
                    for (int j = 0; j < _caracteristicsCount; j++)
                    {
                        inputArr[j] = _moleculesData[i][j];
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

                    if ((output >= 0.5 && _moleculesResults[i]) || (output < 0.5 && !_moleculesResults[i]))
                        fitness += 1.0;

                    // Reset black box state ready for next test case.
                    box.ResetState();
                }

                fitness /= _separation;
                fitness *= 100;
                return new FitnessInfo(fitness, fitness);
            }

            // Test Case
            else
            {
                // Controls if the stop condition is satisfied
                if (_ea.CurrentGeneration == _maxGen + 2)
                {
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
                else
                {
                    for (int t = _separation; t < _moleculesCount; t++)
                    {
                        for (int j = 0; j < _caracteristicsCount; j++)
                        {
                            inputArr[j] = _moleculesData[t][j];
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

                        if (output >= 0.5 && _moleculesResults[t] || output < 0.5 && !_moleculesResults[t])
                            fitness += 1.0;


                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    fitness /= (_moleculesCount - _separation);
                    fitness *= 100;
                    return new FitnessInfo(fitness, fitness);
                }
            }
        }

        /// <summary>
        /// Fitness function case: EscalatedAccuracy. Any correct answer's weight is the inverse of the amount of 
        /// cases of that type in the dataset.
        /// </summary>
        private FitnessInfo EscalatedAccuracy(IBlackBox box)
        {
            double fitness = 0.0;
            double output;
            ISignalArray inputArr = box.InputSignalArray;
            ISignalArray outputArr = box.OutputSignalArray;
            _evalCount++;
            int positives = 0;
            int negatives = 0;

            // If it should analize the training or the test data.
            if (_ea.CurrentGeneration == _maxGen + 1)
            {
                _trainingMode = false;
            }

            // Training mode case
            if (_trainingMode) {
                
                // Counts the amount of each classes cases.
                for (int o = 0; o < _separation; o++)
                    if (_moleculesResults[o])
                        positives++;
                    else
                        negatives++;
                
                for (int i = 0; i < _separation; i++)
                {
                    for (int j = 0; j < _caracteristicsCount; j++)
                    {
                        inputArr[j] = _moleculesData[i][j];
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

                    if (output >= 0.5 && _moleculesResults[i])
                        fitness += 1.0/positives;
                    if (output < 0.5 && !_moleculesResults[i])
                        fitness += 1.0/negatives;
                              


                    // Reset black box state ready for next test case.
                    box.ResetState();
                }

                fitness *= 50;
                return new FitnessInfo(fitness, fitness);
            }
            // Test Case
            else
            {
                // Controls if the stop condition is satisfied
                if (_ea.CurrentGeneration == _maxGen + 2)
                {
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
              else
                {
                    // Counts the amount of each classes cases.
                    for (int p = _separation; p < _moleculesCount; p++)
                        if (_moleculesResults[p])
                            positives++;
                        else
                            negatives++;

                    for (int t = _separation; t < _moleculesCount; t++)
                    {
                        for (int j = 0; j < _caracteristicsCount; j++)
                        {
                            inputArr[j] = _moleculesData[t][j];
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

                        if (output >= 0.5 && _moleculesResults[t])
                            fitness += 1.0 / positives;
                        if (output < 0.5 && !_moleculesResults[t])
                            fitness += 1.0 / negatives;


                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    fitness *= 50;
                    return new FitnessInfo(fitness, fitness);
                }
            }
        }
    }
}
