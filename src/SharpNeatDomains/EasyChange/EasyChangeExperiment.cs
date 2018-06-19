﻿/* ***************************************************************************
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
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using SharpNeat.Core;
using SharpNeat.Decoders;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Network;
using SharpNeat.Phenomes;
using SharpNeat.SpeciationStrategies;

namespace SharpNeat.Domains.EasyChange
{
    /// <summary>
    /// EasyChange task.
    /// </summary>
    public class EasyChangeExperiment : IGuiNeatExperiment
    {
        private static readonly ILog __log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        NeatEvolutionAlgorithmParameters _eaParams;
        NeatGenomeParameters _neatGenomeParams;
        string _name;
        int _populationSize;
        int _specieCount;
        NetworkActivationScheme _activationScheme;
        string _complexityRegulationStr;
        int? _complexityThreshold;
        string _description;
        ParallelOptions _parallelOptions;
        EasyChangeDataLoader _dataLoader;
        int _maxGen;
        double _testPorcentage;
        int _savePeriod;
        int _seed;
        string _datasetPath;
        bool _normalizeData;
        int _normalizeRange;
        int _fitnessFunction;
        double _batchSizePorcentage;
        bool _saveChampStats;




        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public EasyChangeExperiment()
        {

        }

        #endregion

        #region Gets

        public double BatchSizePorcentage
        {
            get { return _batchSizePorcentage; }
            set { _batchSizePorcentage = value; }
        }
        public ParallelOptions ParallelOps
        {
            get { return _parallelOptions; }
            set { _parallelOptions = value; }
        }
        public int FitnessFunction
        {
            get { return _fitnessFunction; }
            set { _fitnessFunction = value; }
        }

        public string DatasetPath
        {
            get { return _datasetPath; }
            set { _datasetPath = value; }
        }
        public int Seed
        {
            get { return _seed;  }
            set { _seed = value; }
        }

        public int SavePeriod
        {
            get { return _savePeriod; }
            set { _savePeriod = value; }
        }
      
        public int MaxGen
        {
            get { return _maxGen; }
            set { _maxGen = value; }
        }

        public double TestPorcentage
        {
            get { return _testPorcentage; }
            set { _testPorcentage = value; }
        }

        public EasyChangeDataLoader DataLoader
        {
            get { return _dataLoader; }
        }

        public bool NormalizeData
        {
            get { return _normalizeData; }
            set { _normalizeData = value; }
        }

        public int NormalizeRange
        {
            get { return _normalizeRange; }
            set { _normalizeRange = value; }
        }

        public bool SaveChampStats
        {
            get { return _saveChampStats; }
            set { _saveChampStats = value; }
        }
        #endregion

        #region INeatExperiment

        /// <summary>
        /// Gets the name of the experiment.
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets human readable explanatory text for the experiment.
        /// </summary>
		public string Description
		{
			get { return _description; }
		}

        /// <summary>
        /// Gets the number of inputs required by the network/black-box that the underlying problem domain is based on.
        /// </summary>
        public int InputCount
        {
            get { return _dataLoader.CaracteristicsCount; }
        }

        /// <summary>
        /// Gets the number of outputs required by the network/black-box that the underlying problem domain is based on.
        /// </summary>
        public int OutputCount
        {
            get { return 1; }
        }

        /// <summary>
        /// Gets the default population size to use for the experiment.
        /// </summary>
        public int DefaultPopulationSize 
        {
            get { return _populationSize; }
        }

        /// <summary>
        /// Gets the NeatEvolutionAlgorithmParameters to be used for the experiment. Parameters on this object can be 
        /// modified. Calls to CreateEvolutionAlgorithm() make a copy of and use this object in whatever state it is in 
        /// at the time of the call.
        /// </summary>
        public NeatEvolutionAlgorithmParameters NeatEvolutionAlgorithmParameters
        {
            get { return _eaParams; }
        }

        /// <summary>
        /// Gets the NeatGenomeParameters to be used for the experiment. Parameters on this object can be modified. Calls
        /// to CreateEvolutionAlgorithm() make a copy of and use this object in whatever state it is in at the time of the call.
        /// </summary>
        public NeatGenomeParameters NeatGenomeParameters
        {
            get { return _neatGenomeParams; }
        }

        /// <summary>
        /// Initialize the experiment with some XML configuration data.
        /// </summary>
        public void Initialize(string name, XmlElement xmlConfig)
        {
            _name = name;
            _populationSize = XmlUtils.GetValueAsInt(xmlConfig, "PopulationSize");
            _specieCount = XmlUtils.GetValueAsInt(xmlConfig, "SpecieCount");
            _activationScheme = ExperimentUtils.CreateActivationScheme(xmlConfig, "Activation");
            _complexityRegulationStr = XmlUtils.TryGetValueAsString(xmlConfig, "ComplexityRegulationStrategy");
            _complexityThreshold = XmlUtils.TryGetValueAsInt(xmlConfig, "ComplexityThreshold");
            _description = XmlUtils.TryGetValueAsString(xmlConfig, "Description");
            _parallelOptions = ExperimentUtils.ReadParallelOptions(xmlConfig);
            _seed = XmlUtils.GetValueAsInt(xmlConfig, "Seed");
            _datasetPath = XmlUtils.TryGetValueAsString(xmlConfig, "DatasetPath");
            _normalizeData = XmlUtils.GetValueAsBool(xmlConfig, "NormalizeData");
            _normalizeRange = XmlUtils.GetValueAsInt(xmlConfig, "NormalizeRange");
            _dataLoader = new EasyChangeDataLoader();
            _eaParams = new NeatEvolutionAlgorithmParameters();
            _eaParams.SpecieCount = _specieCount;
            _neatGenomeParams = new NeatGenomeParameters();
            _neatGenomeParams.FeedforwardOnly = _activationScheme.AcyclicNetwork;
            _neatGenomeParams.ActivationFn = LeakyReLU.__DefaultInstance;
            _maxGen = XmlUtils.GetValueAsInt(xmlConfig, "MaxGen");
            _testPorcentage = XmlUtils.GetValueAsInt(xmlConfig,"TestPorcentage");
            _savePeriod = XmlUtils.GetValueAsInt(xmlConfig, "SavePeriod");
            _batchSizePorcentage = XmlUtils.GetValueAsInt(xmlConfig, "BatchSizePorcentage");

        }

        /// <summary>
        /// Load a population of genomes from an XmlReader and returns the genomes in a new list.
        /// The genome factory for the genomes can be obtained from any one of the genomes.
        /// </summary>
        public List<NeatGenome> LoadPopulation(XmlReader xr)
        {
            NeatGenomeFactory genomeFactory = (NeatGenomeFactory)CreateGenomeFactory();
            return NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, genomeFactory);
        }

        /// <summary>
        /// Special case for prediction:
        /// Load a population of genomes from an XmlReader and returns the genomes in a new list.
        /// The genome factory for the genomes can be obtained from any one of the genomes.
        /// </summary>
        public List<NeatGenome> LoadPopulationPrediction(XmlReader xr, int input, int output)
        {
            NeatGenomeFactory genomeFactory = (NeatGenomeFactory)CreateGenomeFactoryPrediction(input,output);
            return NeatGenomeXmlIO.ReadCompleteGenomeList(xr, false, genomeFactory);
        }

        /// <summary>
        /// Save a population of genomes to an XmlWriter.
        /// </summary>
        public void SavePopulation(XmlWriter xw, IList<NeatGenome> genomeList)
        {
            // Writing node IDs is not necessary for NEAT.
            NeatGenomeXmlIO.WriteComplete(xw, genomeList, false);
        }

        /// <summary>
        /// Create a genome decoder for the experiment.
        /// </summary>
        public IGenomeDecoder<NeatGenome,IBlackBox> CreateGenomeDecoder()
        {
            return new NeatGenomeDecoder(_activationScheme);
        }

        /// <summary>
        /// Create a genome factory for the experiment.
        /// Create a genome factory with our neat genome parameters object and the appropriate number of input and output neuron genes.
        /// </summary>
        public IGenomeFactory<NeatGenome> CreateGenomeFactory()
        {
            // Initialize the dataloader. This is done here because the genomes must be created with a certain amount 
            // of inputs; which is specified in the amount of molecule's caracteristics of each dataset.
            _dataLoader.Initialize( _datasetPath,
                                    _normalizeData,
                                    _normalizeRange,
                                    _seed);
            return new NeatGenomeFactory(InputCount, OutputCount, _neatGenomeParams);
        }

        /// <summary>
        /// Special case for prediction:
        /// Create a genome factory for the experiment.
        /// Create a genome factory with our neat genome parameters object and the appropriate number of input and output neuron genes.
        /// </summary>
        public IGenomeFactory<NeatGenome> CreateGenomeFactoryPrediction(int input, int output)
        {
            return new NeatGenomeFactory(input, output, _neatGenomeParams);
        }

        /// <summary>
        /// Create and return a NeatEvolutionAlgorithm object ready for running the NEAT algorithm/search. Various sub-parts
        /// of the algorithm are also constructed and connected up.
        /// Uses the experiments default population size defined in the experiment's config XML.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm()
        {
            return CreateEvolutionAlgorithm(_populationSize);
        }

        /// <summary>
        /// Create and return a NeatEvolutionAlgorithm object ready for running the NEAT algorithm/search. Various sub-parts
        /// of the algorithm are also constructed and connected up.
        /// This overload accepts a population size parameter that specifies how many genomes to create in an initial randomly
        /// generated population.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(int populationSize)
        {
            // Create a genome factory with our neat genome parameters object and the appropriate number of input and output neuron genes.
            IGenomeFactory<NeatGenome> genomeFactory = CreateGenomeFactory();

            // Create an initial population of randomly generated genomes.
            List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(populationSize, 0);

            // Create evolution algorithm.
            return CreateEvolutionAlgorithm(genomeFactory, genomeList);
        }

        /// <summary>
        /// Create and return a NeatEvolutionAlgorithm object ready for running the NEAT algorithm/search. Various sub-parts
        /// of the algorithm are also constructed and connected up.
        /// This overload accepts a pre-built genome population and their associated/parent genome factory.
        /// </summary>
        public NeatEvolutionAlgorithm<NeatGenome> CreateEvolutionAlgorithm(IGenomeFactory<NeatGenome> genomeFactory, List<NeatGenome> genomeList)
        {
            // Create distance metric. Mismatched genes have a fixed distance of 10; for matched genes the distance is their weight difference.
            IDistanceMetric distanceMetric = new ManhattanDistanceMetric(1.0, 0.0, 10.0);
            ISpeciationStrategy<NeatGenome> speciationStrategy = new ParallelKMeansClusteringStrategy<NeatGenome>(distanceMetric, _parallelOptions);

            // Create complexity regulation strategy.
            IComplexityRegulationStrategy complexityRegulationStrategy = ExperimentUtils.CreateComplexityRegulationStrategy(_complexityRegulationStr, _complexityThreshold);

            // Create the evolution algorithm.
            NeatEvolutionAlgorithm<NeatGenome> ea = new NeatEvolutionAlgorithm<NeatGenome>(_eaParams, speciationStrategy, complexityRegulationStrategy);

            // Create IBlackBox evaluator.
            EasyChangeEvaluator evaluator = new EasyChangeEvaluator(ea,_dataLoader,_maxGen,_testPorcentage,_fitnessFunction, _batchSizePorcentage, _saveChampStats);

            // Create genome decoder. Decodes to a neural network packaged with an activation scheme.
            IGenomeDecoder<NeatGenome, IBlackBox> genomeDecoder =  CreateGenomeDecoder();

            // Create a genome list evaluator. This packages up the genome decoder with the genome evaluator.
            IGenomeListEvaluator<NeatGenome> innerEvaluator = new ParallelGenomeListEvaluator<NeatGenome, IBlackBox>(genomeDecoder, evaluator, _parallelOptions);

            // If we use the complete train set, we don't reevaluate champions.
            if (_batchSizePorcentage == 1)
            {
                // Wrap the list evaluator in a 'selective' evaluator that will only evaluate new genomes. That is, we skip re-evaluating any genomes
                // that were in the population in previous generations (elite genomes). This is determined by examining each genome's evaluation info object.
                IGenomeListEvaluator<NeatGenome> selectiveEvaluator = new SelectiveGenomeListEvaluator<NeatGenome>(
                                                                                        innerEvaluator,
                                                                                        SelectiveGenomeListEvaluator<NeatGenome>.CreatePredicate_CheckForTrainingStatus(ea,_maxGen));
                
                // Initialize the evolution algorithm.
                ea.Initialize(selectiveEvaluator, genomeFactory, genomeList);
            }
            else
                ea.Initialize(innerEvaluator, genomeFactory, genomeList);
            // Finished. Return the evolution algorithm
            return ea;
        }

        /// <summary>
        /// Create a System.Windows.Forms derived object for displaying genomes.
        /// </summary>
        public AbstractGenomeView CreateGenomeView()
        {
            return new NeatGenomeView();
        }

        /// <summary>
        /// Create a System.Windows.Forms derived object for displaying output for a domain (e.g. show best genome's output/performance/behaviour in the domain). 
        /// </summary>
        public AbstractDomainView CreateDomainView()
        {
            return null;
        }
		
        #endregion


    }
}
