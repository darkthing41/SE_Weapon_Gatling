#region Header
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace SpaceEngineersScripting
{
	public class Wrapper
	{
		static void Main()
		{
			new Program().Main ("");
		}
	}


	class Program : Sandbox.ModAPI.Ingame.MyGridProgram
	{
		#endregion
		#region CodeEditor
		//Configuration
		//--------------------

		const string
			nameRotorDrive = "Rotor Drive";     //name of the rotor which spins the chambers

		const string
			nameGunPrefix = "25cm RaK 6 l/40";  //prefix shared by guns
		const uint
			gunCount = 24;  //number of guns, numbered 0..Count
		//Expect Gun names of the form
		//  <Name> ::= <prefix> <Id>
		//  <Id> ::= <integer range 0..gunCount>
		//e.g. First gun to fire
		//25cm RaK 6 l/40 1

		const float //radians 0..2pi ; converted from degrees
			angleTarget = 0.0f * (Pi / 180.0f),     //angle at which the gun should fire
			angleTolerance = 2.0f * (Pi / 180.0f);  //allow guns to fire within this error of the target
			                                        //-recommended ~1.5 degrees for 30rpm Drive


		//Definitions
		//--------------------

		//Opcodes for use as arguments
		//-commands may be issued directly
		const string
			command_Initialise = "Init";   //args: <none>

		//Utility definitions

		const float
			Pi = MathHelper.Pi,
			PiByTwo = Pi * 0.5f,
			ThreePiByTwo = Pi * 1.5f,
			TwoPi = Pi * 2.0f;

		const float
			angleGun = TwoPi / gunCount;

		/// <summary>
		/// Fast normalisation to range of
		/// -pi <= angle < pi
		/// (assuming no more than one cycle out)
		/// </summary>
		static float NormaliseRadians_Pi (float angle){
			if (angle < -Pi)
				return angle +TwoPi;
			if (angle >= Pi)
				return angle -TwoPi;
			return angle;
		}

		/// <summary>
		/// Fast normalisation to range of
		/// 0 <= angle < 2*pi
		/// (assuming no more than one cycle out)
		/// </summary>
		static float NormaliseRadians_2Pi (float angle){
			if (angle < 0.0f)
				return angle +TwoPi;
			if (angle >= TwoPi)
				return angle -TwoPi;
			return angle;
		}


		//Internal Types
		//--------------------

		public struct Status
		{
			//program data not persistent across restarts
			public bool
				initialised;

			//program data persistent across restarts
			public uint
				indexGunLast;   //the last gun fired

			//configuration constants
			private const char
				delimiter = ';';

			//Operations

			public void Initialise(){   //data setup
				indexGunLast = 0;
			}

			public string Store()
			{
				return indexGunLast.ToString();
			}

			public bool TryRestore(string storage)
			{
				string[] elements = storage.Split(delimiter);
				return
					(elements.Length == 1)
					&& uint.TryParse(elements[0], out indexGunLast);
			}
		}


		//Global variables
		//--------------------
		Status
			status;

		IMyMotorStator
			rotorDrive;
		IMyUserControllableGun[]
			guns = new IMyUserControllableGun[gunCount];

		List<IMyTerminalBlock>
			temp = new List<IMyTerminalBlock>();


		//Program
		//--------------------

		public Program()
		{
			Echo ("Restarted.");

			//script has been reloaded
			//-may be first time running
			//-world may have been reloaded (or script recompiled)
			if (Storage == null) {
				//use default values
				status.Initialise();
			} else {
				//attempt to restore saved values
				//  -otherwise use defaults
				Echo ("restoring saved state...");
				if ( !status.TryRestore(Storage) ){
					Echo ("restoration failed.");
					status.Initialise();
				}
			}
			//We are not initialised after restart
			//-attempt to initialise now to reduce load at run-time
			status.initialised = false;
			Initialise();
		}

		public void Save()
		{
			Storage = status.Store();
		}


		public void Main(string argument)
		{
			//First ensure the system is able to process commands
			//-if necessary, perform first time setup
			//-if necessary or requested, initialise the system
			if ( !status.initialised || argument == command_Initialise) {
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}
			if (argument == command_Initialise) {
				Echo ("resetting.");
				status.Initialise ();
			}
			else if ( !Validate() ) {
				//if saved state is not valid, try re-initialising
				//if we cannot initialise, end here
				if ( !Initialise() )
					return;
			}

			//Perform main processing
			Update();

			//Save status back
			//Storage = status.Store();
		}


		private void Update()
		{
			float   //0..2pi
				angle = rotorDrive.Angle,   //assumes drive has unlimited rotor angle; otherwise normalise
				angleTrue = NormaliseRadians_2Pi(angle -angleTarget);

			//Find gun closest to current angle
			uint
				indexGun = (uint)Math.Floor(NormaliseRadians_2Pi(angleTrue +(angleGun/2.0f)) / angleGun);

			//Echo status
			Echo ("angle : " +MathHelper.ToDegrees(angle).ToString("F1"));
			Echo ("angleTrue : " +MathHelper.ToDegrees(angleTrue).ToString("F1"));
			Echo ("gun: " +(indexGun).ToString());

			//See if we could fire that gun
			//-check that is was not the last gun fired
			//-check that it is correctly aligned
			if (indexGun != status.indexGunLast) {
				float
					angleError = Math.Abs(NormaliseRadians_Pi(angleTrue -indexGun*angleGun));

				if (angleError < angleTolerance) {
					guns[indexGun].ApplyAction ("ShootOnce");
					status.indexGunLast = indexGun;

					Echo ("Fired: " +(indexGun).ToString());
				}
			}

		}


		private bool FindBlock<BlockType>(out BlockType block, string nameBlock, ref List<IMyTerminalBlock> temp)
			where BlockType : class, IMyTerminalBlock
		{
			block = null;
			GridTerminalSystem.GetBlocksOfType<BlockType> (temp);
			for (int i=0; i<temp.Count; i++){
				if (temp[i].CustomName == nameBlock) {
					if (block == null) {
						block = (BlockType)temp[i];
					} else {
						Echo ("ERROR: duplicate name \"" +nameBlock +"\"");
						return false;
					}
				}
			}
			//verify that the block was found
			if (block == null) {
				Echo ("ERROR: block not found \"" +nameBlock +"\"");
				return false;
			}
			return true;
		}

		private bool ValidateBlock(IMyTerminalBlock block, bool callbackRequired=true)
		{
			//check for block deletion?

			//check that we have required permissions to control the block
			if ( ! Me.HasPlayerAccess(block.OwnerId) ) {
				Echo ("ERROR: no permissions for \"" +block.CustomName +"\"");
				return false;
			}

			//check that the block has required permissions to make callbacks
			if ( callbackRequired && !block.HasPlayerAccess(Me.OwnerId) ) {
				Echo ("ERROR: no permissions on \"" +block.CustomName +"\"");
				return false;
			}

			//check that block is functional
			if (!block.IsFunctional) {
				Echo ("ERROR: non-functional block \"" +block.CustomName +"\"");
				return false;
			}

			return true;
		}


		private bool Initialise()
		{
			status.initialised = false;
			Echo ("initialising...");

			var temp = new List<IMyTerminalBlock>();

			//Find Drive rotor and verify that it is operable
			if ( !( FindBlock<IMyMotorStator>(out rotorDrive, nameRotorDrive, ref temp)
				&& ValidateBlock(rotorDrive, callbackRequired:false) ))
				return false;

			//Find guns
			GridTerminalSystem.GetBlocksOfType<IMyUserControllableGun>(temp);
			for (uint i=0; i<gunCount; i++) {
				guns[i] = null; //clear any existing reference

				//Find the required Gun
				string nameBlock = nameGunPrefix +" " +i.ToString();
				for (int t=0; t<temp.Count; t++) {
					if (temp[t].CustomName == nameBlock) {
						if (guns[i] == null) {
							guns[i] = (IMyUserControllableGun)temp[t];
						} else {
							Echo ("ERROR: duplicate name \"" + nameBlock + "\"");
							return false;
						}
					}
				}
				//Check that the Gun was found
				if (guns[i] == null) {
					Echo ("ERROR: block not found \"" +nameBlock +"\"");
					return false;
				}
				//Check that the found Gun is operable
				if ( !ValidateBlock(guns[i]) )
					return false;
			}

			status.initialised = true;
			Echo ("Initialisation completed with no errors.");
			return true;
		}


		private bool Validate(){
			bool valid =
				ValidateBlock (rotorDrive, callbackRequired:true);

			for (uint i=0; i<gunCount; i++) {
				valid = valid
					& ValidateBlock(guns[i], callbackRequired:false);
			}

			if ( !valid ) {
				Echo ("Validation of saved blocks failed.");
			}
			return valid;
		}
		#endregion
		#region footer
	}
}
#endregion