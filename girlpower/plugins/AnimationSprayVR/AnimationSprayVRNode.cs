#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Utils.Animation;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "SprayVR", Category = "Animation")]
	#endregion PluginInfo
	public class AnimationSprayVRNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input")]
		public ISpread<Vector3D> FInput;

		[Input("Bang")]
		public ISpread<bool> FBang;

		[Input("Acceleration")]
		public ISpread<double> FAcc;

		[Input("Max Lifetime")]
		public ISpread<double> FMaxLifeTime;

		[Output("Output")]
		public ISpread<Vector3D> FOutput;

		[Output("Age")]
		public ISpread<double> FAge;

		[Import()]
		public ILogger FLogger;

		[Import()]
		public IHDEHost FHDEHost;

		List<Particle> FParticles = new List<Particle>();
		Random FRandom = new Random();
		#endregion fields & pins

		//called each frame by vvvv
		public void Evaluate(int SpreadMax)
		{
			//update particles and remove timed out particles
			for (int i = FParticles.Count - 1; i >= 0; i--)
				if (!FParticles[i].Update(FHDEHost.FrameTime))
					FParticles.RemoveAt(i);

			for (int i = 0; i < SpreadMax; i++) {
				//add new particles
				if (FBang[i]) {
					for (int j = 0; j < 2; j++) {
						var vel = new Vector3D(FRandom.NextDouble() - 0.5, FRandom.NextDouble() - 0.5, FRandom.NextDouble() - 0.5);
						vel.z = 0;
						var acc = new Vector3D(0, FAcc[i], FAcc[i]);
						FParticles.Add(new Particle(FHDEHost.FrameTime, FMaxLifeTime[i], FInput[i], vel*0.2, acc));
					}
				}
			}

			//set outputs
			FOutput.SliceCount = FParticles.Count;
			FAge.SliceCount = FParticles.Count;
			for (int i = 0; i < FParticles.Count; i++) {
				FOutput[i] = FParticles[i].Position;
				FAge[i] = FParticles[i].Age;
			}
		}
	}
}