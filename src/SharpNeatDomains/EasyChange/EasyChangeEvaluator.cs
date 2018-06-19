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
using System.Threading;
using Redzen.Random;
using System.IO;
using System.Globalization;

namespace SharpNeat.Domains.EasyChange
{
    /// <summary>
    /// Evaluator for an EasyChange experiment.
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
        static int _split;
        static int _fitnessFunction;
        static double _batchSize;
        static bool _saveChampStats;
        Semaphore _sem;
        int _lastGeneration;
        int _positives;
        int _negatives;
        int _index;
        protected readonly IRandomSource _rnd;
        double _champFitnessTest;
        long[,] _champConfMatrix;
        bool _saved;


        #region IPhenomeEvaluator<IBlackBox> Members

        public EasyChangeEvaluator(NeatEvolutionAlgorithm<NeatGenome> ea, EasyChangeDataLoader dataLoader, int maxGen, double testPorcentage, int fitnessFunction,double batchSizePorcentage, bool saveChampStats)
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
            _split = (int)temp;
            _fitnessFunction = fitnessFunction;
            temp = _split * batchSizePorcentage;
            _batchSize = (int) temp;
            _sem = new Semaphore(1, 1);
            _lastGeneration = -1;
            _rnd = RandomSourceFactory.Create();
            _champConfMatrix = new long[2,2];
            _champFitnessTest = -1;
            _saved = false;
            _saveChampStats = saveChampStats;


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
            else if (_fitnessFunction == 1)
                return EscalatedAccuracy(box);
            else
                return MCC(box);
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
                _sem.WaitOne();
                if (_ea.CurrentGeneration > _lastGeneration)
                {
                    _index = _rnd.Next(0, _split);
                    _lastGeneration = (int)_ea.CurrentGeneration;
                }
                _sem.Release();

                for (int i = 0; i < _batchSize; i++)
                {
                    for (int j = 0; j < _caracteristicsCount; j++)
                    {
                        inputArr[j] = _moleculesData[(i + _index) % _split][j];
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

                    if ((output >= 1 && _moleculesResults[(i + _index) % _split]) || (output < 1 && !_moleculesResults[(i + _index) % _split]))
                        fitness += 1.0;

                    // Reset black box state ready for next test case.
                    box.ResetState();
                }

                fitness /= _batchSize;
                fitness *= 100;
                return new FitnessInfo(fitness, fitness);
            }

            // Test Case
            else
            {
                // Controls if the stop condition is satisfied
                if (_ea.CurrentGeneration == _maxGen + 2)
                {
                    _sem.WaitOne();
                    if (_saveChampStats && !_saved)
                    {
                        SaveStats();
                        _saved = true;
                    }
                    _sem.Release();
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
                else
                {
                    // Confution matrix of current genome
                    long[,] confMatrix = new long[2,2];

                    for (int t = _split; t < _moleculesCount; t++)
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

                        // Full the confution matrix
                        confMatrix[_moleculesResults[t] ? 1 : 0 , (output >= 1) ? 1 : 0] += 1;

                        // Evaluation of prediction
                        if (output >= 1 && _moleculesResults[t] || output < 1 && !_moleculesResults[t])
                            fitness += 1.0;


                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    fitness /= (_moleculesCount - _split);
                    fitness *= 100;

                    // Check if current genome is champ
                    _sem.WaitOne();
                    if (fitness > _champFitnessTest)
                    {
                        _champFitnessTest = fitness;
                        _champConfMatrix = confMatrix;
                    }
                    _sem.Release();

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


            // If it should analize the training or the test data.
            if (_ea.CurrentGeneration == _maxGen + 1)
            {
                _trainingMode = false;
            }

            // Training mode case
            if (_trainingMode) {
                _sem.WaitOne();
                if (_ea.CurrentGeneration > _lastGeneration)
                {
                    
                    // Checks there are at least one element of each class
                    do
                    {
                        _positives = 0;
                        _negatives = 0;
                        _index = _rnd.Next(0, _split);
                        //_index = ((int)_batchSize + _index) % _split;

                        // Counts the amount of each classes cases.
                        for (int o = 0; o < _batchSize; o++)
                            if (_moleculesResults[(o + _index) % _split])
                                _positives++;
                            else
                                _negatives++;
                    }
                    while (_positives == 0 || _negatives == 0);

                    _lastGeneration = (int)_ea.CurrentGeneration;
                }
                _sem.Release();
                for (int i = 0; i < _batchSize; i++)
                {
                    for (int j = 0; j < _caracteristicsCount; j++)
                    {
                        inputArr[j] = _moleculesData[(i + _index)% _split][j];
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

                    if (output >= 1 && _moleculesResults[(i + _index) % _split])
                        fitness += 1.0/_positives;
                    if (output < 1 && !_moleculesResults[(i + _index) % _split])
                        fitness += 1.0/_negatives;
                              


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
                    _sem.WaitOne();
                    if (_saveChampStats && !_saved)
                    {
                        SaveStats();
                        _saved = true;
                    }
                    _sem.Release();
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
              else
                {
                    _sem.WaitOne();
                    if (_ea.CurrentGeneration > _lastGeneration)
                    {
                        _positives = 0;
                        _negatives = 0;
                        // Counts the amount of each classes cases.
                        for (int p = _split; p < _moleculesCount; p++)
                            if (_moleculesResults[p])
                                _positives++;
                            else
                                _negatives++;
                        _lastGeneration = (int)_ea.CurrentGeneration;
                        
                    }
                    _sem.Release();

                    // Confution matrix of current genome
                    long[,] confMatrix = new long[2,2];

                    // Full input 
                    for (int t = _split; t < _moleculesCount; t++)
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

                        //Full the confution matrix
                        confMatrix[_moleculesResults[t] ? 1 : 0 , (output >= 1) ? 1 : 0] += 1;

                        // Evaluation of result
                        if (output >= 1 && _moleculesResults[t])
                            fitness += 1.0 / _positives;
                        if (output < 1 && !_moleculesResults[t])
                            fitness += 1.0 / _negatives;

                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    fitness *= 50;

                    // Check if current genome is champ
                    _sem.WaitOne();
                    if(fitness > _champFitnessTest) {
                        _champFitnessTest = fitness;
                        _champConfMatrix = confMatrix;
                    }                        
                    _sem.Release();

                    return new FitnessInfo(fitness, fitness);
                }
            }
        }


        /// <summary>
        /// Fitness function case: Matthews Correlation Coefficient
        /// </summary>
        private FitnessInfo MCC(IBlackBox box)
        {
            double fitness = 0.0;
            double output;
            ISignalArray inputArr = box.InputSignalArray;
            ISignalArray outputArr = box.OutputSignalArray;
            _evalCount++;

            // Confution matrix of current genome
            long[,] confMatrix = new long[2, 2];

            // If it should analize the training or the test data.
            if (_ea.CurrentGeneration == _maxGen + 1)
            {
                _trainingMode = false;
            }

            // Training mode case
            if (_trainingMode)
            {
                _sem.WaitOne();
                if (_ea.CurrentGeneration > _lastGeneration)
                {
                    _index = _rnd.Next(0, _split);
                    _lastGeneration = (int)_ea.CurrentGeneration;
                }
                _sem.Release();

                for (int i = 0; i < _batchSize; i++)
                {
                    for (int j = 0; j < _caracteristicsCount; j++)
                    {
                        inputArr[j] = _moleculesData[(i + _index) % _split][j];
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

                    // Full the confution matrix
                    confMatrix[_moleculesResults[i] ? 1 : 0, (output >= 1) ? 1 : 0] += 1;

                    // Reset black box state ready for next test case.
                    box.ResetState();
                }

                // MCC calculation
                long TP = confMatrix[1, 1];
                long FP = confMatrix[0, 1];
                long TN = confMatrix[0, 0];
                long FN = confMatrix[1, 0];

                long denominator = (TP + FP) * (TP + FN) * (TN + FP) * (TN + FN);
                if (denominator == 0)
                    denominator = 1;
                fitness = (TP * TN - FP * FN) / Math.Sqrt(denominator);
                
                // Linear Transformation                
                fitness += 1;
                //fitness *= 50;

                return new FitnessInfo(fitness, fitness);
            }

            // Test Case
            else
            {
                // Controls if the stop condition is satisfied
                if (_ea.CurrentGeneration == _maxGen + 2)
                {
                    _sem.WaitOne();
                    if (_saveChampStats && !_saved)
                    {
                        SaveStats();
                        _saved = true;
                    }
                    _sem.Release();
                    _stopConditionSatisfied = true;
                    return FitnessInfo.Zero;
                }
                else
                {
                    
                    for (int t = _split; t < _moleculesCount; t++)
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

                        // Full the confution matrix
                        confMatrix[_moleculesResults[t] ? 1 : 0, (output >= 1) ? 1 : 0] += 1;

                        // Reset black box state ready for next test case.
                        box.ResetState();
                    }

                    // MCC calculation
                    long TP = confMatrix[1, 1];
                    long FP = confMatrix[0, 1];
                    long TN = confMatrix[0, 0];
                    long FN = confMatrix[1, 0];

                    long denominator = (TP + FP) * (TP + FN) * (TN + FP) * (TN + FN);
                    if (denominator == 0)
                        denominator = 1;
                    fitness = (TP * TN - FP * FN) / Math.Sqrt(denominator);

                    // Linear Transformation                
                    fitness += 1;
                    //fitness *= 50;

                    // Check if current genome is champ
                    _sem.WaitOne();
                    if (fitness > _champFitnessTest)
                    {
                        _champFitnessTest = fitness;
                        _champConfMatrix = confMatrix;
                    }
                    _sem.Release();

                    return new FitnessInfo(fitness, fitness);
                }
            }
        }

        /// <summary>
        /// Function responsable for saving the champs stats during testing mode if specified.
        /// </summary>
        private void SaveStats() {

            // Save format.
            NumberFormatInfo filenameNumberFormatter = new NumberFormatInfo();
            filenameNumberFormatter.NumberDecimalSeparator = ",";

            // Save champs stats
            string path = string.Format(filenameNumberFormatter, "Stats/Stats_Fit{0:0.00}_{1:HHmmss_ddMMyyyy}.txt",
                                            _champFitnessTest, DateTime.Now);

            StreamWriter writer = new StreamWriter(path);

            writer.WriteLine("Total number of cases in test dataset :{0}", _moleculesCount - _split);
            writer.WriteLine();
            writer.WriteLine("Testing champion fitness:{0}",_champFitnessTest);
            writer.WriteLine();
            writer.WriteLine("Champions confution matrix (Rows = Real Value) (Columns = Predicted Value):");
            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < 2; i++)
                {
                    if (i != 0)
                    {
                    writer.Write(" ");
                    }
                writer.Write(_champConfMatrix[i,j]);
                }
            writer.WriteLine();
            }

            writer.Close();
        }
    }
}
