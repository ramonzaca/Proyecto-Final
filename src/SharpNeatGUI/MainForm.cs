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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Xml;

using log4net;
using log4net.Config;
using Redzen.Numerics;
using SharpNeat.Core;
using SharpNeat.Domains;
using SharpNeat.Domains.EasyChange;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;
using SharpNeat.Utility;

namespace SharpNeatGUI
{
    /// <summary>
    /// SharpNEAT main GUI window.
    /// </summary>
    public partial class MainForm : Form
    {
        private static readonly ILog __log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region Instance Fields [General]

        EasyChangeExperiment _selectedExperiment;
        IGenomeFactory<NeatGenome> _genomeFactory;
        List<NeatGenome> _genomeList;
        NeatEvolutionAlgorithm<NeatGenome> _ea;
        StreamWriter _logFileWriter = null;
        /// <summary>Number format for building filename when saving champ genomes.</summary>
        NumberFormatInfo _filenameNumberFormatter;
        /// <summary>XmlWriter settings for champ genome saving (format the XML to make it human readable).</summary>
        XmlWriterSettings _xwSettings;
        /// <summary>Tracks the best champ fitness observed so far.</summary>
        double _champGenomeFitness;
        /// <summary>Array of 'nice' colors for chart plots.</summary>
        Color[] _plotColorArr;

        #endregion

        #region Instance Fields [Views]

        GenomeForm _bestGenomeForm;
        ProblemDomainForm _domainForm;
        List<TimeSeriesGraphForm> _timeSeriesGraphFormList = new List<TimeSeriesGraphForm>();
        List<SummaryGraphForm> _summaryGraphFormList = new List<SummaryGraphForm>();

        // Working storage space for graph views.
        static int[] _specieDataArrayInt;
        static Point2DDouble[] _specieDataPointArrayInt;
        
        static double[] _specieDataArray;
        static Point2DDouble[] _specieDataPointArray;

        static double[] _genomeDataArray;
        static Point2DDouble[] _genomeDataPointArray;

        #endregion

        #region Form Constructor / Initialisation

        /// <summary>
        /// Construct and initialize the form.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            Logger.SetListBox(lbxLog);
            XmlConfigurator.Configure(new FileInfo("log4net.properties"));
            InitProblemDomainList();

            _filenameNumberFormatter = new NumberFormatInfo();
            _filenameNumberFormatter.NumberDecimalSeparator = ",";

            _xwSettings = new XmlWriterSettings();
            _xwSettings.Indent = true;

            _plotColorArr = GenerateNiceColors(10);
        }

        /// <summary>
        /// Initialise the problem domain combobox. The list of problem domains is read from an XML file; this 
        /// allows changes to be made and new domains to be plugged-in without recompiling binaries.
        /// </summary>
        private void InitProblemDomainList()
        {
            List<ExperimentInfo> expInfoList = new List<ExperimentInfo>();
            // Find all experiment config data files in the current directory (*.experiments.xml)
            foreach (string filename in Directory.EnumerateFiles(".", "*.experiments.xml"))
            {
                expInfoList = ExperimentInfo.ReadExperimentXml(filename);
                
            }

            ExperimentInfo expInfo = expInfoList[0];

            Assembly assembly = Assembly.LoadFrom(expInfo.AssemblyPath);
            // TODO: Handle non-gui experiments.
            _selectedExperiment = assembly.CreateInstance(expInfo.ClassName) as EasyChangeExperiment;
            _selectedExperiment.Initialize(expInfo.Name, expInfo.XmlConfig);

            // Load cmb

            foreach (string dataset in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/../../../Data/"))
            {
              
                cmbExperiments.Items.Add(new ListItem(string.Empty, dataset.Split('/')[dataset.Split('/').Length - 1], dataset));
            }
            // Pre-select first item.
            cmbExperiments.SelectedIndex = 0;
            btnLoadDomainDefaults_Click(null, null);

            cmbNormalizeData.Items.Add(new ListItem(string.Empty, "True", true));
            cmbNormalizeData.Items.Add(new ListItem(string.Empty, "False", false));
            cmbNormalizeData.SelectedIndex = 0;

            cmbFitnessFnc.Items.Add(new ListItem(string.Empty, "Accuracy", 0));
            cmbFitnessFnc.Items.Add(new ListItem(string.Empty, "Escalated Accuracy", 1));
            cmbFitnessFnc.SelectedIndex = 0;

        }

        #endregion

        #region GUI Wiring [Experiment Selection + Default Param Loading]

        private void cmbExperiments_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Se reinicializa el dataloader del experimento con el nuevo path
                experiment.DatasetPath = ((string) ((ListItem)cmbExperiments.SelectedItem).Data);
                UpdateGuiState();
            }
            catch (Exception ex)
            {
                __log.ErrorFormat("Error loading dataset. Error message [{0}]", ex.Message);
            }
            
            // Close any experiment specific forms that remain open.
            if(null != _bestGenomeForm) {
                _bestGenomeForm.Close();
                _bestGenomeForm = null;
            }

            if(null != _domainForm) {
                _domainForm.Close();
                _domainForm = null;
            }
            
        }

        private void btnExperimentInfo_Click(object sender, EventArgs e)
        {
            if(null == cmbExperiments.SelectedItem) {
                return;
            }

            EasyChangeExperiment experiment = GetSelectedExperiment();
            if(null != experiment) {
                MessageBox.Show(experiment.Description);
            }
        }

        private void btnLoadDomainDefaults_Click(object sender, EventArgs e)
        {
            // Dump the experiment's default parameters into the GUI.
            EasyChangeExperiment experiment = GetSelectedExperiment();
            txtParamPopulationSize.Text = experiment.DefaultPopulationSize.ToString();

            NeatEvolutionAlgorithmParameters eaParams = experiment.NeatEvolutionAlgorithmParameters;
            NeatGenomeParameters ngParams = experiment.NeatGenomeParameters;
            txtParamInitialConnectionProportion.Text = ngParams.InitialInterconnectionsProportion.ToString();
            txtParamNumberOfSpecies.Text = eaParams.SpecieCount.ToString();
            txtParamElitismProportion.Text = eaParams.ElitismProportion.ToString();
            txtParamSelectionProportion.Text = eaParams.SelectionProportion.ToString();
            txtParamOffspringAsexual.Text = eaParams.OffspringAsexualProportion.ToString();
            txtParamOffspringCrossover.Text = eaParams.OffspringSexualProportion.ToString();
            txtParamInterspeciesMating.Text = eaParams.InterspeciesMatingProportion.ToString();
            txtParamConnectionWeightRange.Text = ngParams.ConnectionWeightRange.ToString();
            txtParamMutateConnectionWeights.Text = ngParams.ConnectionWeightMutationProbability.ToString();
            txtParamMutateAddNode.Text = ngParams.AddNodeMutationProbability.ToString();
            txtParamMutateAddConnection.Text = ngParams.AddConnectionMutationProbability.ToString();
            txtParamMutateDeleteConnection.Text = ngParams.DeleteConnectionMutationProbability.ToString();
        }

        private EasyChangeExperiment GetSelectedExperiment()
        {
           return _selectedExperiment;
        }

        #endregion

        #region GUI Wiring [Population Init]

        private void btnCreateRandomPop_Click(object sender, EventArgs e)
        {
            // Parse population size and interconnection proportion from GUI fields.
            int? popSize = ParseInt(txtParamPopulationSize);
            if(null == popSize) {
                return;
            }

            double? initConnProportion = ParseDouble(txtParamInitialConnectionProportion);
            if(null == initConnProportion) {
                return;
            }

            EasyChangeExperiment experiment = GetSelectedExperiment();
            experiment.NeatGenomeParameters.InitialInterconnectionsProportion = initConnProportion.Value;

            // Create a genome factory appropriate for the experiment.
            IGenomeFactory<NeatGenome> genomeFactory = experiment.CreateGenomeFactory();
                
            // Create an initial population of randomly generated genomes.
            List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(popSize.Value, 0u);

            // Assign population to form variables & update GUI appropriately.
            _genomeFactory = genomeFactory;
            _genomeList = genomeList;
            UpdateGuiState();
        }

        #endregion

        #region GUI Wiring [Algorithm Init/Start/Stop]

        private void btnSearchStart_Click(object sender, EventArgs e)
        {
            if(null != _ea)
            {   // Resume existing EA & update GUI state.
                _ea.StartContinue();
                UpdateGuiState();
                return;
            }

            // Initialise and start a new evolution algorithm.
            ReadAndUpdateExperimentParams();

            // Check number of species is <= the number of the genomes.
            if(_genomeList.Count < _selectedExperiment.NeatEvolutionAlgorithmParameters.SpecieCount) {
                __log.ErrorFormat("Genome count must be >= specie count. Genomes=[{0}] Species=[{1}]",
                                    _selectedExperiment.NeatEvolutionAlgorithmParameters.SpecieCount, _genomeList.Count);
                return;
            }

            // Create evolution algorithm.
            _ea = _selectedExperiment.CreateEvolutionAlgorithm(_genomeFactory, _genomeList);

            // Attach update event listener.
            _ea.UpdateEvent += new EventHandler(_ea_UpdateEvent);
            _ea.PausedEvent += new EventHandler(_ea_PausedEvent);

            // Notify any open views.
            if(null != _bestGenomeForm) { _bestGenomeForm.Reconnect(_ea); }
            if(null != _domainForm) { _domainForm.Reconnect(_ea); }
            foreach(TimeSeriesGraphForm graphForm in _timeSeriesGraphFormList) {
                graphForm.Reconnect(_ea);
            }
            foreach(SummaryGraphForm graphForm in _summaryGraphFormList) {
                graphForm.Reconnect(_ea);
            }

            // Create/open log file if the option is selected.
            if(chkFileWriteLog.Checked && null==_logFileWriter)
            {
                string filename = txtFileLogBaseName.Text + '_' + DateTime.Now.ToString("yyyyMMdd") + ".log";
                _logFileWriter = new StreamWriter(filename, true);
                _logFileWriter.WriteLine("ClockTime,Gen,BestFitness,MeanFitness,MeanSpecieChampFitness,ChampComplexity,MeanComplexity,MaxComplexity,TotalEvaluationCount,EvaluationsPerSec,SearchMode");
            }

            // Start the algorithm & update GUI state.
            _ea.StartContinue();
            UpdateGuiState();
        }

        private void btnSearchStop_Click(object sender, EventArgs e)
        {
            _ea.RequestPause();

            if(null != _logFileWriter)
            {
                // Null _logFileWriter prior to closing the writer. This much reduced the chance of attempt to write to the stream after it has closed.
                StreamWriter sw = _logFileWriter;
                _logFileWriter = null;
                sw.Close();
            }
        }

        private void btnSearchReset_Click(object sender, EventArgs e)
        {
            _genomeFactory = null;
            _genomeList = null;
            // TODO: Proper cleanup of EA - e.g. main worker thread termination.
            _ea = null;
            _champGenomeFitness = 0.0;
            Logger.Clear();
            UpdateGuiState_ResetStats();
            UpdateGuiState();
        }

        /// <summary>
        /// Read experimental parameters from the GUI and update _selectedExperiment with the read values.
        /// </summary>
        private void ReadAndUpdateExperimentParams()
        {
            NeatEvolutionAlgorithmParameters eaParams = _selectedExperiment.NeatEvolutionAlgorithmParameters;
            eaParams.SpecieCount = ParseInt(txtParamNumberOfSpecies, eaParams.SpecieCount);
            eaParams.ElitismProportion = ParseDouble(txtParamElitismProportion, eaParams.ElitismProportion);
            eaParams.SelectionProportion = ParseDouble(txtParamSelectionProportion, eaParams.SelectionProportion);
            eaParams.OffspringAsexualProportion = ParseDouble(txtParamOffspringAsexual, eaParams.OffspringAsexualProportion);
            eaParams.OffspringSexualProportion = ParseDouble(txtParamOffspringCrossover, eaParams.OffspringSexualProportion);
            eaParams.InterspeciesMatingProportion = ParseDouble(txtParamInterspeciesMating, eaParams.InterspeciesMatingProportion);

            NeatGenomeParameters ngParams = _selectedExperiment.NeatGenomeParameters;
            ngParams.ConnectionWeightRange = ParseDouble(txtParamConnectionWeightRange, ngParams.ConnectionWeightRange);
            ngParams.ConnectionWeightMutationProbability = ParseDouble(txtParamMutateConnectionWeights, ngParams.ConnectionWeightMutationProbability);
            ngParams.AddNodeMutationProbability = ParseDouble(txtParamMutateAddNode, ngParams.AddNodeMutationProbability);
            ngParams.AddConnectionMutationProbability = ParseDouble(txtParamMutateAddConnection, ngParams.AddConnectionMutationProbability);
            ngParams.DeleteConnectionMutationProbability = ParseDouble(txtParamMutateDeleteConnection, ngParams.DeleteConnectionMutationProbability);
            _selectedExperiment.MaxGen = ParseInt(txtMaxGen, _selectedExperiment.MaxGen);
            _selectedExperiment.TestPorcentage = ParseDouble(txtTestPorcentage, _selectedExperiment.TestPorcentage) / 100;
            _selectedExperiment.SavePeriod = ParseInt(txtSavePeriod, _selectedExperiment.SavePeriod);
            _selectedExperiment.NormalizeData = (bool)((ListItem)cmbNormalizeData.SelectedItem).Data;
            _selectedExperiment.NormalizeRange = ParseInt(txtNormalizeRange, _selectedExperiment.NormalizeRange);
            _selectedExperiment.Seed = ParseInt(txtSeed, _selectedExperiment.Seed);
        }

        #endregion

        #region GUI Wiring [GUI State Updating]

        private void UpdateGuiState()
        {
            if(null == _ea)
            {
                if(null == _genomeList) {
                    UpdateGuiState_NoPopulation();
                }
                else {
                    UpdateGuiState_PopulationReady();
                }
            }
            else
            {
                switch(_ea.RunState)
                {
                    case RunState.Ready:
                    case RunState.Paused:
                        UpdateGuiState_EaReadyPaused();
                        break;
                    case RunState.Running:
                        UpdateGuiState_EaRunning();
                        break;
                    default:
                        throw new ApplicationException($"Unexpected RunState [{_ea.RunState}]");
                }
            }
        }

        private void UpdateGuiState_NoPopulation()
        {
            // Enable experiment selection and initialization buttons.
            cmbExperiments.Enabled = true;
            btnLoadDomainDefaults.Enabled = true;
            btnCreateRandomPop.Enabled = true;

            // Display population status (empty).
            txtPopulationStatus.Text = "Population not initialized";
            txtPopulationStatus.BackColor = Color.Red;

            // Disable search control buttons.
            btnSearchStart.Enabled = false;
            btnSearchStop.Enabled = false;
            btnSearchReset.Enabled = false;

            // Parameter fields enabled.
            txtParamNumberOfSpecies.Enabled = true;
            txtParamPopulationSize.Enabled = true;
            txtParamInitialConnectionProportion.Enabled = true;
            txtParamElitismProportion.Enabled = true;
            txtParamSelectionProportion.Enabled = true;
            txtParamOffspringAsexual.Enabled = true;
            txtParamOffspringCrossover.Enabled = true;
            txtParamInterspeciesMating.Enabled = true;
            txtParamConnectionWeightRange.Enabled = true;
            txtParamMutateConnectionWeights.Enabled = true;
            txtParamMutateAddNode.Enabled = true;
            txtParamMutateAddConnection.Enabled = true;
            txtParamMutateDeleteConnection.Enabled = true;
            txtMaxGen.Enabled = true;
            txtSavePeriod.Enabled= true;
            txtTestPorcentage.Enabled = true;
            cmbNormalizeData.Enabled = true;
            if (cmbNormalizeData.SelectedIndex == 0)
                txtNormalizeRange.Enabled = true;
            else
                txtNormalizeRange.Enabled = false;
            txtSeed.Enabled = true;
            cmbFitnessFnc.Enabled = true;

            // Logging to file.
            gbxLogging.Enabled = true;

            // Menu bar (file).
            loadPopulationToolStripMenuItem.Enabled = true;
            loadSeedGenomesToolStripMenuItem.Enabled = true;
            loadSeedGenomeToolStripMenuItem.Enabled = true;
            savePopulationToolStripMenuItem.Enabled = false;
            saveBestGenomeToolStripMenuItem.Enabled = false;
            toolStripMenuItem1.Enabled = true;
        }

        private void UpdateGuiState_PopulationReady()
        {
            // Disable anything to do with initialization now that we are initialized.
            cmbExperiments.Enabled = false;
            btnLoadDomainDefaults.Enabled = false;
            btnCreateRandomPop.Enabled = false;

            // Display how many genomes & status.
            txtPopulationStatus.Text = $"{_genomeList.Count:D0} genomes ready";
            txtPopulationStatus.BackColor = Color.Orange;

            // Enable search control buttons.
            btnSearchStart.Enabled = true;
            btnSearchStop.Enabled = false;
            btnSearchReset.Enabled = true;

            // Parameter fields enabled (apart from population creation params)
            txtParamNumberOfSpecies.Enabled = false;
            txtParamPopulationSize.Enabled = false;
            txtParamInitialConnectionProportion.Enabled = false;
            txtParamElitismProportion.Enabled = true;
            txtParamSelectionProportion.Enabled = true;
            txtParamOffspringAsexual.Enabled = true;
            txtParamOffspringCrossover.Enabled = true;
            txtParamInterspeciesMating.Enabled = true;
            txtParamConnectionWeightRange.Enabled = true;
            txtParamMutateConnectionWeights.Enabled = true;
            txtParamMutateAddNode.Enabled = true;
            txtParamMutateAddConnection.Enabled = true;
            txtParamMutateDeleteConnection.Enabled = true;
            txtMaxGen.Enabled = true;
            txtSavePeriod.Enabled = true;
            txtTestPorcentage.Enabled = true;
            cmbNormalizeData.Enabled = false;
            txtNormalizeRange.Enabled = false;
            txtSeed.Enabled = false;
            cmbFitnessFnc.Enabled = true;




            // Logging to file.
            gbxLogging.Enabled = true;

            // Menu bar.
            loadPopulationToolStripMenuItem.Enabled = false;
            loadSeedGenomesToolStripMenuItem.Enabled = false;
            loadSeedGenomeToolStripMenuItem.Enabled = false;
            savePopulationToolStripMenuItem.Enabled = true;
            saveBestGenomeToolStripMenuItem.Enabled = false;
            toolStripMenuItem1.Enabled = false;
            
        }

        /// <summary>
        /// Evolution algorithm is ready/paused.
        /// </summary>
        private void UpdateGuiState_EaReadyPaused()
        {
            // Disable anything to do with initialization now that we are initialized.
            cmbExperiments.Enabled = false;
            btnLoadDomainDefaults.Enabled = false;
            btnCreateRandomPop.Enabled = false;

            // Display how many genomes & status.
            txtPopulationStatus.Text = $"{_genomeList.Count:D0} genomes paused.";
            txtPopulationStatus.BackColor = Color.Orange;

            // Search control buttons.
            btnSearchStart.Enabled = true;
            btnSearchStop.Enabled = false;
            btnSearchReset.Enabled = true;

            // Parameter fields (disable).
            txtParamNumberOfSpecies.Enabled = false;
            txtParamPopulationSize.Enabled = false;
            txtParamInitialConnectionProportion.Enabled = false;
            txtParamElitismProportion.Enabled = false;
            txtParamSelectionProportion.Enabled = false;
            txtParamOffspringAsexual.Enabled = false;
            txtParamOffspringCrossover.Enabled = false;
            txtParamInterspeciesMating.Enabled = false;
            txtParamConnectionWeightRange.Enabled = false;
            txtParamMutateConnectionWeights.Enabled = false;
            txtParamMutateAddNode.Enabled = false;
            txtParamMutateAddConnection.Enabled = false;
            txtParamMutateDeleteConnection.Enabled = false;
            txtMaxGen.Enabled = false;
            txtSavePeriod.Enabled = false;
            txtTestPorcentage.Enabled = false;
            cmbNormalizeData.Enabled = false;
            txtNormalizeRange.Enabled = false;
            txtSeed.Enabled = false;
            cmbFitnessFnc.Enabled = false;


            // Logging to file.
            gbxLogging.Enabled = true;

            // Menu bar.
            loadPopulationToolStripMenuItem.Enabled = false;
            loadSeedGenomesToolStripMenuItem.Enabled = false;
            loadSeedGenomeToolStripMenuItem.Enabled = false;
            savePopulationToolStripMenuItem.Enabled = true;
            saveBestGenomeToolStripMenuItem.Enabled = (_ea.CurrentChampGenome != null);
            toolStripMenuItem1.Enabled = false;
        }

        /// <summary>
        /// Evolution algorithm is running.
        /// </summary>
        private void UpdateGuiState_EaRunning()
        {
            // Disable anything to do with initialization now that we are initialized.
            cmbExperiments.Enabled = false;
            btnLoadDomainDefaults.Enabled = false;
            btnCreateRandomPop.Enabled = false;

            // Display how many genomes & status.
            txtPopulationStatus.Text = $"{_genomeList.Count:D0} genomes running";
            txtPopulationStatus.BackColor = Color.LightGreen;

            // Search control buttons.
            btnSearchStart.Enabled = false;
            btnSearchStop.Enabled = true;
            btnSearchReset.Enabled = false;

            // Parameter fields (disable).
            txtParamNumberOfSpecies.Enabled = false;
            txtParamPopulationSize.Enabled = false;
            txtParamInitialConnectionProportion.Enabled = false;
            txtParamElitismProportion.Enabled = false;
            txtParamSelectionProportion.Enabled = false;
            txtParamOffspringAsexual.Enabled = false;
            txtParamOffspringCrossover.Enabled = false;
            txtParamInterspeciesMating.Enabled = false;
            txtParamConnectionWeightRange.Enabled = false;
            txtParamMutateConnectionWeights.Enabled = false;
            txtParamMutateAddNode.Enabled = false;
            txtParamMutateAddConnection.Enabled = false;
            txtParamMutateDeleteConnection.Enabled = false;
            txtMaxGen.Enabled = false;
            txtSavePeriod.Enabled = false;
            txtTestPorcentage.Enabled = false;
            cmbNormalizeData.Enabled = false;
            txtNormalizeRange.Enabled = false;
            txtSeed.Enabled = false;
            cmbFitnessFnc.Enabled = false;


            // Logging to file.
            gbxLogging.Enabled = false;

            // Menu bar.
            loadPopulationToolStripMenuItem.Enabled = false;
            loadSeedGenomesToolStripMenuItem.Enabled = false;
            loadSeedGenomeToolStripMenuItem.Enabled = false;
            savePopulationToolStripMenuItem.Enabled = false;
            saveBestGenomeToolStripMenuItem.Enabled = false;
            toolStripMenuItem1.Enabled = false;
        }

        private void UpdateGuiState_EaStats()
        {
            NeatAlgorithmStats stats = _ea.Statistics;
            txtSearchStatsMode.Text = _ea.ComplexityRegulationMode.ToString();
            switch( _ea.ComplexityRegulationMode)
            {
                case ComplexityRegulationMode.Complexifying:
                    txtSearchStatsMode.BackColor = Color.LightSkyBlue;
                    break;
                case ComplexityRegulationMode.Simplifying:
                    txtSearchStatsMode.BackColor = Color.Tomato;
                    break;
            }

            txtStatsGeneration.Text = _ea.CurrentGeneration.ToString("N0");
            txtStatsBest.Text = stats._maxFitness.ToString();

            // Auxiliary fitness info.
            AuxFitnessInfo[] auxFitnessArr = _ea.CurrentChampGenome.EvaluationInfo.AuxFitnessArr;
            if(auxFitnessArr.Length > 0) {
                txtStatsAlternativeFitness.Text = auxFitnessArr[0]._value.ToString("#.######");
            } else {
                txtStatsAlternativeFitness.Text = "";
            }

            txtStatsMean.Text = stats._meanFitness.ToString("#.######");
            txtSpecieChampMean.Text = stats._meanSpecieChampFitness.ToString("#.######");
            txtStatsTotalEvals.Text = stats._totalEvaluationCount.ToString("N0");
            txtStatsEvalsPerSec.Text = stats._evaluationsPerSec.ToString("##,#.##");
            txtStatsBestGenomeComplx.Text = _ea.CurrentChampGenome.Complexity.ToString("N0");
            txtStatsMeanGenomeComplx.Text = stats._meanComplexity.ToString("#.##");
            txtStatsMaxGenomeComplx.Text = stats._maxComplexity.ToString("N0");

            ulong totalOffspringCount = stats._totalOffspringCount;
            txtStatsTotalOffspringCount.Text = totalOffspringCount.ToString("N0");
            txtStatsAsexualOffspringCount.Text = string.Format("{0:N0} ({1:P})", stats._asexualOffspringCount, ((double)stats._asexualOffspringCount/(double)totalOffspringCount));
            txtStatsCrossoverOffspringCount.Text = string.Format("{0:N0} ({1:P})", stats._sexualOffspringCount, ((double)stats._sexualOffspringCount/(double)totalOffspringCount));
            txtStatsInterspeciesOffspringCount.Text = string.Format("{0:N0} ({1:P})", stats._interspeciesOffspringCount, ((double)stats._interspeciesOffspringCount/(double)totalOffspringCount));
        }

        private void UpdateGuiState_ResetStats()
        {
            txtSearchStatsMode.Text = string.Empty;
            txtSearchStatsMode.BackColor = Color.LightSkyBlue;
            txtStatsGeneration.Text = string.Empty;
            txtStatsBest.Text = string.Empty;
            txtStatsAlternativeFitness.Text= string.Empty;
            txtStatsMean.Text = string.Empty;
            txtSpecieChampMean.Text = string.Empty;
            txtStatsTotalEvals.Text = string.Empty;
            txtStatsEvalsPerSec.Text = string.Empty;
            txtStatsBestGenomeComplx.Text =string.Empty;
            txtStatsMeanGenomeComplx.Text = string.Empty;
            txtStatsMaxGenomeComplx.Text = string.Empty;            
            txtStatsTotalOffspringCount.Text = string.Empty;
            txtStatsAsexualOffspringCount.Text = string.Empty;
            txtStatsCrossoverOffspringCount.Text = string.Empty;
            txtStatsInterspeciesOffspringCount.Text = string.Empty;
        }

        #endregion

        #region GUI Wiring [Menu Bar - Population & Genome Loading/Saving]

        private void loadPopulationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string popFilePath = SelectFileToOpen("Load population", "pop.xml", "(*.pop.xml)|*.pop.xml");
            if(string.IsNullOrEmpty(popFilePath)) {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Load population of genomes from file.
                List<NeatGenome> genomeList;
                using(XmlReader xr = XmlReader.Create(popFilePath)) 
                {
                    genomeList = experiment.LoadPopulation(xr);
                }

                if(genomeList.Count == 0) {
                    __log.WarnFormat("No genomes loaded from population file [{0}]", popFilePath);
                    return;
                }

                // Assign genome list and factory to class variables and update GUI.
                _genomeFactory = genomeList[0].GenomeFactory;
                _genomeList = genomeList;
                UpdateGuiState();
            }
            catch(Exception ex)
            {
                __log.ErrorFormat("Error loading population. Error message [{0}]", ex.Message);
            }
        }

        private void loadSeedGenomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filePath = SelectFileToOpen("Load seed genome", "gnm.xml", "(*.gnm.xml)|*.gnm.xml");
            if(string.IsNullOrEmpty(filePath)) {
                return;
            }

            // Parse population size from GUI field.
            int? popSize = ParseInt(txtParamPopulationSize);
            if(null == popSize) {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Load genome from file.
                List<NeatGenome> genomeList;
                using(XmlReader xr = XmlReader.Create(filePath)) 
                {
                    genomeList = experiment.LoadPopulation(xr);
                }

                if(genomeList.Count == 0) {
                    __log.WarnFormat("No genome loaded from file [{0}]", filePath);
                    return;
                }

                // Create genome list from seed, assign to local variables and update GUI.
                _genomeFactory = genomeList[0].GenomeFactory;
                _genomeList = _genomeFactory.CreateGenomeList(popSize.Value, 0u, genomeList[0]);
                UpdateGuiState();
            }
            catch(Exception ex)
            {
                __log.ErrorFormat("Error loading seed genome. Error message [{0}]", ex.Message);
            }
        }

        private void loadSeedGenomesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string popFilePath = SelectFileToOpen("Load seed genomes", "pop.xml", "(*.pop.xml)|*.pop.xml");
            if(string.IsNullOrEmpty(popFilePath)) {
                return;
            }

            // Parse population size from GUI field.
            int? popSize = ParseInt(txtParamPopulationSize);
            if(null == popSize) {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Load genome from file.
                List<NeatGenome> genomeList;
                using(XmlReader xr = XmlReader.Create(popFilePath)) 
                {
                    genomeList = experiment.LoadPopulation(xr);
                }

                if(genomeList.Count == 0) {
                    __log.WarnFormat("No seed genomes loaded from file [{0}]", popFilePath);
                    return;
                }

                // Create genome list from seed genomes, assign to local variables and update GUI.
                _genomeFactory = genomeList[0].GenomeFactory;
                _genomeList = _genomeFactory.CreateGenomeList(popSize.Value, 0u, genomeList);
                UpdateGuiState();
            }
            catch(Exception ex)
            {
                __log.ErrorFormat("Error loading seed genomes. Error message [{0}]", ex.Message);
            }
        }

        private void savePopulationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string popFilePath = SelectFileToSave("Save population", "pop.xml", "(*.pop.xml)|*.pop.xml");
            if(string.IsNullOrEmpty(popFilePath)) {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Save genomes to xml file.
                using(XmlWriter xw = XmlWriter.Create(popFilePath, _xwSettings))
                {
                    experiment.SavePopulation(xw, _genomeList);
                }
            }
            catch(Exception ex)
            {
                __log.ErrorFormat("Error saving population. Error message [{0}]", ex.Message);
            }
        }

        private void saveBestGenomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filePath = SelectFileToSave("Save champion genome", "gnm.xml", "(*.gnm.xml)|*.gnm.xml");
            if(string.IsNullOrEmpty(filePath)) {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Save genome to xml file.
                using(XmlWriter xw = XmlWriter.Create(filePath, _xwSettings))
                {
                    experiment.SavePopulation(xw, new NeatGenome[] {_ea.CurrentChampGenome});
                }
            }
            catch(Exception ex)
            {
                __log.ErrorFormat("Error saving genome. Error message [{0}]", ex.Message);
            }
        }

        // Load Dataset
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string filePath = SelectFileToOpen("Load dataset", ".csv", "(*.csv)|*.csv");
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            try
            {
                // Get the currently selected experiment.
                EasyChangeExperiment experiment = GetSelectedExperiment();

                // Se reinicializa el dataloader del experimento con el nuevo path
                experiment.DatasetPath = filePath;            
                cmbExperiments.Items.Add(new ListItem(string.Empty, filePath.Split('\\')[filePath.Split('\\').Length - 1], filePath));
                cmbExperiments.SelectedIndex = cmbExperiments.Items.Count - 1 ;
                UpdateGuiState();
            }
            catch (Exception ex)
            {
                __log.ErrorFormat("Error loading dataset. Error message [{0}]", ex.Message);
            }
        }
        #endregion

        #region GUI Wiring [Menu Bar - Views]

        private void cmbNormalizeData_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGuiState();
        }

        private void bestGenomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EasyChangeExperiment experiment = GetSelectedExperiment();
            AbstractGenomeView genomeView = experiment.CreateGenomeView();
            if(null == genomeView) {
                return;
            }

            // Create form.
            _bestGenomeForm = new GenomeForm("Best Genome", genomeView, _ea);

            // Attach a event handler to update this main form when the genome form is closed.
            _bestGenomeForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _bestGenomeForm = null;
                bestGenomeToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            bestGenomeToolStripMenuItem.Enabled = false;

            // Show the form.
            _bestGenomeForm.Show(this);
            _bestGenomeForm.RefreshView();
        }

        private void problemDomainToolStripMenuItem_Click(object sender, EventArgs e)
        {
            EasyChangeExperiment experiment = GetSelectedExperiment();
            AbstractDomainView domainView = experiment.CreateDomainView();
            if(null == domainView) {
                return;
            }

            // Create form.
            _domainForm = new ProblemDomainForm(experiment.Name, domainView, _ea);

            // Attach a event handler to update this main form when the domain form is closed.
            _domainForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _domainForm = null;
                problemDomainToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            problemDomainToolStripMenuItem.Enabled = false;

            // Show the form.
            _domainForm.Show(this);
            _domainForm.RefreshView();
        }

        #endregion

        #region GUI Wiring [Menu Bar - Views - Graphs]

        private void fitnessBestMeansToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            // Create data sources.
            List<TimeSeriesDataSource> _dsList = new List<TimeSeriesDataSource>();


            _dsList.Add(new TimeSeriesDataSource("Best", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Red, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._maxFitness);
                                                            }));

            _dsList.Add(new TimeSeriesDataSource("Mean", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Black, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._meanFitness);
                                                            }));

            _dsList.Add(new TimeSeriesDataSource("Best (Moving Average)", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Orange, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._bestFitnessMA.Mean);
                                                            }));

            // Create a data sources for any auxiliary fitness info.
            AuxFitnessInfo[] auxFitnessArr = _ea.CurrentChampGenome.EvaluationInfo.AuxFitnessArr;
            if(null != auxFitnessArr)
            {
                for(int i=0; i<auxFitnessArr.Length; i++)
                {
                    // 'Capture' the value of i in a locally defined variable that has scope specific to each delegate creation (below). If capture 'i' instead then it will always have
                    // its last value in each delegate (which happens to be one past the end of the array).
                    int ii = i;
                    _dsList.Add(new TimeSeriesDataSource(_ea.CurrentChampGenome.EvaluationInfo.AuxFitnessArr[i]._name, TimeSeriesDataSource.DefaultHistoryLength, 0, _plotColorArr[i % _plotColorArr.Length], delegate() 
                                                                    {   
                                                                        return new Point2DDouble(_ea.CurrentGeneration, _ea.CurrentChampGenome.EvaluationInfo.AuxFitnessArr[ii]._value);
                                                                    }));
                }
            }

            // Create form.
            TimeSeriesGraphForm graphForm = new TimeSeriesGraphForm("Fitness (Best and Mean)", "Generation", "Fitness", string.Empty, _dsList.ToArray(), _ea);
            _timeSeriesGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _timeSeriesGraphFormList.Remove(senderObj as TimeSeriesGraphForm);
                fitnessBestMeansToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            fitnessBestMeansToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }        

        private void complexityBestMeansToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            // Create data sources.
            TimeSeriesDataSource dsBestCmplx = new TimeSeriesDataSource("Best", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Red, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.CurrentChampGenome.Complexity);
                                                            });

            TimeSeriesDataSource dsMeanCmplx = new TimeSeriesDataSource("Mean", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Black, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._meanComplexity);
                                                            });

            TimeSeriesDataSource dsMeanCmplxMA = new TimeSeriesDataSource("Mean (Moving Average)", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Orange, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._complexityMA.Mean);
                                                            });

            // Create form.
            TimeSeriesGraphForm graphForm = new TimeSeriesGraphForm("Complexity (Best and Mean)", "Generation", "Complexity", string.Empty,
                                                 new TimeSeriesDataSource[] {dsBestCmplx, dsMeanCmplx, dsMeanCmplxMA}, _ea);
            _timeSeriesGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _timeSeriesGraphFormList.Remove(senderObj as TimeSeriesGraphForm);
                complexityBestMeansToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            complexityBestMeansToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void evaluationsPerSecToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            // Create data sources.
            TimeSeriesDataSource dsEvalsPerSec= new TimeSeriesDataSource("Evals Per Sec", TimeSeriesDataSource.DefaultHistoryLength, 0, Color.Black, delegate() 
                                                            {
                                                                return new Point2DDouble(_ea.CurrentGeneration, _ea.Statistics._evaluationsPerSec);
                                                            });
            // Create form.
            TimeSeriesGraphForm graphForm = new TimeSeriesGraphForm("Evaluations Per Second", "Generation", "Evaluations", string.Empty,
                                                 new TimeSeriesDataSource[] {dsEvalsPerSec}, _ea);
            _timeSeriesGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _timeSeriesGraphFormList.Remove(senderObj as TimeSeriesGraphForm);
                evaluationsPerSecToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            evaluationsPerSecToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieSizeByRankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieSizeRank = new SummaryDataSource("Specie Size", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArrayInt || _specieDataArrayInt.Length != specieCount) {
                                            _specieDataArrayInt = new int[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArrayInt[i] = _ea.SpecieList[i].GenomeList.Count;
                                        }

                                        // Build/create _specieSizePointArray from the _specieSizeArray.
                                        UpdateRankedDataPoints(_specieDataArrayInt, ref _specieDataPointArrayInt);

                                        // Return plot points.
                                        return _specieDataPointArrayInt;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Size by Rank", "Species (largest to smallest)", "Size", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieSizeRank}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieSizeByRankToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieSizeByRankToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieChampFitnessByRankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieChampFitnessRank = new SummaryDataSource("Specie Fitness (Champs)", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie fitnesses into the data array.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].GenomeList[0].EvaluationInfo.Fitness;
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_specieDataArray, ref _specieDataPointArray);

                                        // Return plot points.
                                        return _specieDataPointArray;
                                    });

            SummaryDataSource dsSpecieMeanFitnessRank = new SummaryDataSource("Specie Fitness (Means)", 0, Color.Black, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie fitnesses into the data array.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].CalcMeanFitness();
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_specieDataArray, ref _specieDataPointArray);

                                        // Return plot points.
                                        return _specieDataPointArray;
                                    });


            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Fitness by Rank", "Species", "Fitness", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieChampFitnessRank, dsSpecieMeanFitnessRank}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieChampFitnessByRankToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieChampFitnessByRankToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieChampComplexityByRankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieChampComplexityRank = new SummaryDataSource("Specie Complexity (Champs)", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie complexity values into the data array.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].GenomeList[0].Complexity;
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_specieDataArray, ref _specieDataPointArray);

                                        // Return plot points.
                                        return _specieDataPointArray;
                                    });

            SummaryDataSource dsSpecieMeanComplexityRank = new SummaryDataSource("Specie Complexity (Means)", 0, Color.Black, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie complexity values into the data array.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].CalcMeanComplexity();
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_specieDataArray, ref _specieDataPointArray);

                                        // Return plot points.
                                        return _specieDataPointArray;
                                    });


            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Complexity by Rank", "Species", "Complexity", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieChampComplexityRank, dsSpecieMeanComplexityRank}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieChampComplexityByRankToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieChampComplexityByRankToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void genomeFitnessByRankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsGenomeFitnessRank = new SummaryDataSource("Genome Fitness", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int genomeCount = _ea.GenomeList.Count;
                                        if(null == _genomeDataArray || _genomeDataArray.Length != genomeCount) {
                                            _genomeDataArray = new double[genomeCount];
                                        }

                                        // Copy genome fitness values into the data array.
                                        for(int i=0; i<genomeCount; i++) {
                                            _genomeDataArray[i] = _ea.GenomeList[i].EvaluationInfo.Fitness;
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_genomeDataArray, ref _genomeDataPointArray);

                                        // Return plot points.
                                        return _genomeDataPointArray;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Genome Fitness by Rank", "Genomes", "Fitness", string.Empty,
                                                 new SummaryDataSource[] {dsGenomeFitnessRank}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                genomeFitnessByRankToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            genomeFitnessByRankToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void genomeComplexityByRankToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsGenomeComplexityRank = new SummaryDataSource("Genome Complexity", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int genomeCount = _ea.GenomeList.Count;
                                        if(null == _genomeDataArray || _genomeDataArray.Length != genomeCount) {
                                            _genomeDataArray = new double[genomeCount];
                                        }

                                        // Copy genome complexity values into the data array.
                                        for(int i=0; i<genomeCount; i++) {
                                            _genomeDataArray[i] = _ea.GenomeList[i].Complexity;
                                        }

                                        // Build/create point array.
                                        UpdateRankedDataPoints(_genomeDataArray, ref _genomeDataPointArray);

                                        // Return plot points.
                                        return _genomeDataPointArray;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Genome Complexity by Rank", "Genomes", "Complexity", string.Empty,
                                                 new SummaryDataSource[] {dsGenomeComplexityRank}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                genomeComplexityByRankToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            genomeComplexityByRankToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieSizeDistributionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieSizeDist = new SummaryDataSource("Specie Size Histogram", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].GenomeList.Count;
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_specieDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Size Frequency Histogram", "Species Size", "Frequency", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieSizeDist}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieSizeDistributionToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieSizeDistributionToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieFitnessDistributionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieChampFitnessDist = new SummaryDataSource("Specie Fitness Histogram (Champ)", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].GenomeList[0].EvaluationInfo.Fitness;
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_specieDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });

            SummaryDataSource dsSpecieMeanFitnessDist = new SummaryDataSource("Specie Fitness Histogram (Mean)", 0, Color.Black, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].CalcMeanFitness();
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_specieDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Fitness Histogram", "Fitness", "Frequency", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieChampFitnessDist, dsSpecieMeanFitnessDist}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieFitnessDistributionsToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieFitnessDistributionsToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void specieComplexityDistributionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsSpecieChampComplexityDist = new SummaryDataSource("Specie Complexity Histogram (Champ)", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].GenomeList[0].Complexity;
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_specieDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });

            SummaryDataSource dsSpecieMeanComplexityDist = new SummaryDataSource("Specie Complexity Histogram (Mean)", 0, Color.Black, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int specieCount = _ea.SpecieList.Count;
                                        if(null == _specieDataArray || _specieDataArray.Length != specieCount) {
                                            _specieDataArray = new double[specieCount];
                                        }

                                        // Copy specie sizes into _specieSizeArray.
                                        for(int i=0; i<specieCount; i++) {
                                            _specieDataArray[i] = _ea.SpecieList[i].CalcMeanComplexity();
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_specieDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Specie Complexity Histogram", "Complexity", "Frequency", string.Empty,
                                                 new SummaryDataSource[] {dsSpecieChampComplexityDist, dsSpecieMeanComplexityDist}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                specieComplexityDistributionsToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            specieComplexityDistributionsToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void genomeFitnessDistributionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsGenomeFitnessDist = new SummaryDataSource("Genome Fitness Histogram", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int genomeCount = _ea.GenomeList.Count;
                                        if(null == _genomeDataArray || _genomeDataArray.Length != genomeCount) {
                                            _genomeDataArray = new double[genomeCount];
                                        }

                                        // Copy genome fitness values into the data array.
                                        for(int i=0; i<genomeCount; i++) {
                                            _genomeDataArray[i] = _ea.GenomeList[i].EvaluationInfo.Fitness;
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_genomeDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Genome Fitness Histogram", "Fitness", "Frequency", string.Empty,
                                                 new SummaryDataSource[] {dsGenomeFitnessDist}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                genomeFitnessDistributionToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            genomeFitnessDistributionToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        private void genomeComplexityDistributionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(null == _ea) return;

            SummaryDataSource dsGenomeComplexityDist = new SummaryDataSource("Genome Complexity Histogram", 0, Color.Red, delegate()
                                    {
                                        // Ensure temp working storage is ready.
                                        int genomeCount = _ea.GenomeList.Count;
                                        if(null == _genomeDataArray || _genomeDataArray.Length != genomeCount) {
                                            _genomeDataArray = new double[genomeCount];
                                        }

                                        // Copy genome fitness values into the data array.
                                        for(int i=0; i<genomeCount; i++) {
                                            _genomeDataArray[i] = _ea.GenomeList[i].Complexity;
                                        }

                                        // Calculate a frequency distribution and retrieve it as an array of plottable points.
                                        Point2DDouble[] pointArr = CalcDistributionDataPoints(_genomeDataArray);

                                        // Return plot points.
                                        return pointArr;
                                    });
            // Create form.
            SummaryGraphForm graphForm = new SummaryGraphForm("Genome Complexity Histogram", "Complexity", "Frequency", string.Empty,
                                                 new SummaryDataSource[] {dsGenomeComplexityDist}, _ea);
            _summaryGraphFormList.Add(graphForm);

            // Attach a event handler to update this main form when the graph form is closed.
            graphForm.FormClosed += new FormClosedEventHandler(delegate(object senderObj, FormClosedEventArgs eArgs)
            {
                _summaryGraphFormList.Remove(senderObj as SummaryGraphForm);
                genomeComplexityDistributionToolStripMenuItem.Enabled = true;
            });

            // Prevent creating more then one instance of the form.
            genomeComplexityDistributionToolStripMenuItem.Enabled = false;

            // Show the form.
            graphForm.Show(this);
        }

        #endregion

        #region GUI Wiring [Misc Menu Bar & Button Event Handlers]

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form frmAboutBox = new AboutForm();
            frmAboutBox.ShowDialog(this);
        }

        private void btnCopyLogToClipboard_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach(Logger.LogItem item in lbxLog.Items) {
                sb.AppendLine(item.Message);
            }

            if(sb.Length > 0) {
                Clipboard.SetText(sb.ToString());
            }
        }

        #endregion

        #region GUI Wiring [Update/Pause Event Handlers + Form Close Handler]

        void _ea_UpdateEvent(object sender, EventArgs e)
        {
            // Handle writing to log window. Switch execution to GUI thread if necessary.
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate ()
                {
                    // Update stats on screen.
                    UpdateGuiState_EaStats();

                    // Write entry to log window.
                    __log.Info(string.Format("gen={0:N0} bestFitness={1:N6}", _ea.CurrentGeneration, _ea.Statistics._maxFitness));

                    // Check if we should save the champ genome to a file.
                    NeatGenome champGenome = _ea.CurrentChampGenome;
                    if (chkFileSaveGenomeOnImprovement.Checked && champGenome.EvaluationInfo.Fitness > _champGenomeFitness)
                    {
                        _champGenomeFitness = champGenome.EvaluationInfo.Fitness;
                        string filename = string.Format(_filenameNumberFormatter, "{0}_{1:0.00}_{2:yyyyMMdd_HHmmss}.gnm.xml",
                                                        txtFileBaseName.Text, _champGenomeFitness, DateTime.Now);

                        // Get the currently selected experiment.
                        EasyChangeExperiment experiment = GetSelectedExperiment();

                        // Save genome to xml file.
                        using (XmlWriter xw = XmlWriter.Create(filename, _xwSettings))
                        {
                            experiment.SavePopulation(xw, new NeatGenome[] { champGenome });
                        }
                    }
                }));
            }

            // Handle writing to log file.
            if (null != _logFileWriter)
            {
                NeatAlgorithmStats stats = _ea.Statistics;
                _logFileWriter.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss.fff},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                                        DateTime.Now,
                                                        stats._generation,
                                                        stats._maxFitness,
                                                        stats._meanFitness,
                                                        stats._meanSpecieChampFitness,
                                                        _ea.CurrentChampGenome.Complexity,
                                                        stats._meanComplexity,
                                                        stats._maxComplexity,
                                                        stats._totalEvaluationCount,
                                                        stats._evaluationsPerSec,
                                                        _ea.ComplexityRegulationMode));
                _logFileWriter.Flush();
            }
            // Get the currently selected experiment.
            EasyChangeExperiment exp = GetSelectedExperiment();

            //Save Population (Funciona cuando le pinta)
            if ((_ea.CurrentGeneration % exp.SavePeriod) == 0  && _ea.CurrentGeneration > 0)
            {
               
                NeatAlgorithmStats stats = _ea.Statistics;
                string file = string.Format(_filenameNumberFormatter, "../../../Poblaciones/pop{0}_Seed{1}_Gen{2}_Fit{3:0.00}_{4:HHmmss_ddMMyyyy}.pop.xml",
                                                _ea.GenomeList.Count, exp.Seed, _ea.CurrentGeneration, stats._maxFitness, DateTime.Now);

                // Save genomes to xml file.
                using (XmlWriter xw = XmlWriter.Create(file, _xwSettings))
                {
                    exp.SavePopulation(xw, _genomeList);
                }
            }
        }

        void _ea_PausedEvent(object sender, EventArgs e)
        {
            if(this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(delegate() 
                {
                    UpdateGuiState();
                }));
            }
        }

        /// <summary>
        /// Gracefully handle application exit request.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(null != _ea && _ea.RunState == RunState.Running)
            {
                DialogResult result = MessageBox.Show("Evolution algorithm is still running. Exit anyway?", "Exit?", MessageBoxButtons.YesNo);
                if(result == DialogResult.No)
                {   // Cancel closing of application.
                    e.Cancel = true;
                    return;
                }
            }

            if(null != _ea)
            {
                // Detach event handlers to prevent logging attempts to GUI as it is being torn down.
                _ea.UpdateEvent -= new EventHandler(_ea_UpdateEvent);
                _ea.PausedEvent -= new EventHandler(_ea_PausedEvent);

                if(_ea.RunState == RunState.Running)
                {
                    // Request algorithm to stop but don't wait.
                    _ea.RequestPause();
                }

                // Close log file.
                if(null != _logFileWriter)
                {
                    _logFileWriter.Close();
                    _logFileWriter = null;
                }
            }
        }

        #endregion

        #region Private Methods [Misc Helper Methods]

		/// <summary>
		/// Ask the user for a filename / path.
		/// </summary>
		private string SelectFileToOpen(string dialogTitle, string fileExtension, string filter)
		{
			OpenFileDialog oDialog = new OpenFileDialog();
			oDialog.AddExtension = true;
			oDialog.DefaultExt = fileExtension;
            oDialog.Filter = filter;
			oDialog.Title = dialogTitle;
			oDialog.RestoreDirectory = true;

            // Show dialog and block until user selects a file.
			if(oDialog.ShowDialog() == DialogResult.OK) {
				return oDialog.FileName;
            } 
            // No selection.
            return null;
		}

		/// <summary>
		/// Ask the user for a filename / path.
		/// </summary>
		private string SelectFileToSave(string dialogTitle, string fileExtension, string filter)
		{
			SaveFileDialog oDialog = new SaveFileDialog();
			oDialog.AddExtension = true;
			oDialog.DefaultExt = fileExtension;
            oDialog.Filter = filter;
			oDialog.Title = dialogTitle;
			oDialog.RestoreDirectory = true;

            // Show dialog and block until user selects a file.
			if(oDialog.ShowDialog() == DialogResult.OK) {
				return oDialog.FileName;
            } 
            // No selection.
            return null;
		}

        private int? ParseInt(TextBox txtBox)
        {
            int val;
            if(int.TryParse(txtBox.Text, out val))
            {
                return val;
            }
            __log.ErrorFormat("Error parsing value of text field [{0}]", txtBox.Name);
            return null;
        }

        private int ParseInt(TextBox txtBox, int defaultVal)
        {
            int val;
            if(int.TryParse(txtBox.Text, out val))
            {
                return val;
            }
            __log.ErrorFormat("Error parsing value of text field [{0}]", txtBox.Name);
            return defaultVal;
        }

        private double? ParseDouble(TextBox txtBox)
        {
            double val;
            if(double.TryParse(txtBox.Text, out val))
            {
                return val;
            }
            __log.ErrorFormat("Error parsing value of text field [{0}]", txtBox.Name);
            return null;
        }

        private double ParseDouble(TextBox txtBox, double defaultVal)
        {
            double val;
            if(double.TryParse(txtBox.Text, out val))
            {
                return val;
            }
            __log.ErrorFormat("Error parsing value of text field [{0}]", txtBox.Name);
            return defaultVal;
        }

        /// <summary>
        /// Updates an Point2DDouble array by sorting an array of values and copying the sorted values over the existing values in pointArr.
        /// Optionally creates the Point2DDouble array if it is null or is the wrong size.
        /// </summary>
        private void UpdateRankedDataPoints(int[] valArr, ref Point2DDouble[] pointArr)
        {

            // Sort values (largest first).
            Array.Sort(valArr, delegate(int x, int y)
            {
                if(x > y) {
                    return -1;
                }
                if(x < y) {
                    return 1;
                }
                return 0;
            });

            // Ensure point cache is ready.
            if(null == pointArr || pointArr.Length != valArr.Length)
            {
                pointArr = new Point2DDouble[valArr.Length];
                for(int i=0; i<valArr.Length; i++)  
                {
                    pointArr[i].X = i;
                    pointArr[i].Y = valArr[i];
                }
            }
            else
            {   // Copy sorted values into _specieSizePointArray.
                for(int i=0; i<valArr.Length; i++) {
                    pointArr[i].Y = valArr[i];
                }
            }
        }

        /// <summary>
        /// Updates an Point2DDouble array by sorting an array of values and copying the sorted values over the existing values in pointArr.
        /// Optionally creates the Point2DDouble array if it is null or is the wrong size.
        /// </summary>
        private void UpdateRankedDataPoints(double[] valArr, ref Point2DDouble[] pointArr)
        {

            // Sort values (largest first).
            Array.Sort(valArr, delegate(double x, double y)
            {
                if(x > y) {
                    return -1;
                }
                if(x < y) {
                    return 1;
                }
                return 0;
            });

            // Ensure point cache is ready.
            if(null == pointArr || pointArr.Length != valArr.Length)
            {
                pointArr = new Point2DDouble[valArr.Length];
                for(int i=0; i<valArr.Length; i++)  
                {
                    pointArr[i].X = i;
                    pointArr[i].Y = valArr[i];
                }
            }
            else
            {   // Copy sorted values into _specieSizePointArray.
                for(int i=0; i<valArr.Length; i++) {
                    pointArr[i].Y = valArr[i];
                }
            }
        }

        private Point2DDouble[] CalcDistributionDataPoints(double[] valArr)
        {
            // Square root is a fairly good choice for automatically determining the category count based on number of values being analysed.
            // See http://en.wikipedia.org/wiki/Histogram (section: Number of bins and width).
            int categoryCount = (int)Math.Sqrt(valArr.Length);
            HistogramData hd = NumericsUtils.BuildHistogramData(valArr, categoryCount);

            // Create array of distribution plot points.
            Point2DDouble[] pointArr = new Point2DDouble[hd.FrequencyArray.Length];
            double incr = hd.Increment;
            double x = hd.Min + (incr/2.0);

            for(int i=0; i<hd.FrequencyArray.Length; i++, x+=incr)
            {
                pointArr[i].X = x;
                pointArr[i].Y = hd.FrequencyArray[i];
            }
            return pointArr;
        }

        private Color[] GenerateNiceColors(int count)
        {
            Color[] arr = new Color[count];
            Color baseColor = ColorTranslator.FromHtml("#8A56E2");
            double baseHue = (new HSLColor(baseColor)).Hue;

            List<Color> colorList = new List<Color>();
            colorList.Add(baseColor);

            double step = (240.0 / (double)count);
            for (int i=1; i < count; i++)
            {
                HSLColor nextColor = new HSLColor(baseColor);
                nextColor.Hue = (baseHue + step * ((double)i)) % 240.0;
                colorList.Add((Color)nextColor);
            }
            return colorList.ToArray();
        }




        #endregion

        private void cmbFitnessFnc_SelectedIndexChanged(object sender, EventArgs e)
        {
            EasyChangeExperiment exp = GetSelectedExperiment();
            exp.FitnessFunction = cmbFitnessFnc.SelectedIndex;
            UpdateGuiState();
            
        }
    }
}
