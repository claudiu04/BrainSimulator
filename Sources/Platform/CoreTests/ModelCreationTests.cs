﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoodAI.Core;
using GoodAI.Core.Execution;
using GoodAI.Core.Nodes;
using GoodAI.Core.Task;
using GoodAI.Core.Utils;
using GoodAI.Modules.NeuralNetwork.Group;
using GoodAI.Modules.NeuralNetwork.Layers;
using GoodAI.Modules.Scripting;
using GoodAI.Modules.Testing;
using GoodAI.Modules.Transforms;
using MNIST;
using Xunit;

namespace CoreTests
{
    public class ModelCreationTests
    {
        [Fact]
        public void SimulationRunsViaRunner()
        {
            var runner = new MyProjectRunner();
            MyProject project = MyProjectRunner.CreateProject(typeof(MyTestingWorld), "test project name");

            var node = project.CreateNode<MyCSharpNode>();
            project.Network.AddChild(node);

            runner.RunAndPause(10);

            runner.Shutdown();
        }

        [Fact]
        public void CreatesAndRunsMNIST()
        {
            var runner = new MyProjectRunner();
            MyProject project = MyProjectRunner.CreateProject(typeof(MyMNISTWorld), "MNIST");

            MyWorld world = project.World;

            var neuralGroup = project.CreateNode<MyNeuralNetworkGroup>();
            project.Network.AddChild(neuralGroup);

            var hiddenLayer = project.CreateNode<MyHiddenLayer>();
            neuralGroup.AddChild(hiddenLayer);

            var outputLayer = project.CreateNode<MyOutputLayer>();
            neuralGroup.AddChild(outputLayer);

            var accumulator = project.CreateNode<MyAccumulator>();
            neuralGroup.AddChild(accumulator);

            // Connect the nodes.

            project.Connect(project.Network.GroupInputNodes[0], neuralGroup, 0, 0);
            project.Connect(project.Network.GroupInputNodes[1], neuralGroup, 0, 1);

            project.Connect(neuralGroup.GroupInputNodes[0], hiddenLayer, 0, 0);

            project.Connect(neuralGroup.GroupInputNodes[1], outputLayer, 0, 1);

            project.Connect(hiddenLayer, outputLayer, 0, 0);

            project.Connect(outputLayer, accumulator, 1, 0);

            // Setup the nodes.

            MyTask sendMnistData = world.GetTaskByPropertyName("SendMNISTData");
            sendMnistData.GetType().GetProperty("RandomEnumerate").SetValue(sendMnistData, true);
            sendMnistData.GetType().GetProperty("ExpositionTime").SetValue(sendMnistData, 1);

            world.GetType().GetProperty("Binary").SetValue(world, true);

            hiddenLayer.Neurons = 40;

            accumulator.ApproachValue.ApproachMethod = MyAccumulator.MyApproachValueTask.SequenceType.Momentum;
            accumulator.ApproachValue.Delta = 0.1f;
            accumulator.ApproachValue.Target = 0;
            accumulator.ApproachValue.Factor = 0.9f;

            // Enable tasks.

            project.World.EnableAllTasks();

            neuralGroup.RMS.Enabled = true;
            neuralGroup.InitGroup.Enabled = true;
            neuralGroup.IncrementTimeStep.Enabled = true;
            neuralGroup.DecrementTimeStep.Enabled = true;

            hiddenLayer.EnableAllTasks();
            hiddenLayer.ShareWeightsTask.Enabled = false;

            outputLayer.EnableAllTasks();
            outputLayer.ShareWeightsTask.Enabled = false;

            accumulator.ApproachValue.Enabled = true;

            // Run the simulation.

            runner.RunAndPause(100);

            //accumulator.Output.SafeCopyToHost();
            //float error = accumulator.Output.GetValueAt(0);
            float error = runner.GetValues(accumulator.Id)[0];
            Assert.True(error < 0.5f);
            //runner.SaveProject(@"c:\foobar.brain");

            runner.Shutdown();
        }
    }
}
