﻿using System;
using OpenBveApi.Math;
using OpenBveApi.Colors;
using System.Collections.Generic;

namespace OpenBve
{
	internal partial class Bve5ScenarioParser
	{
		private static void ApplyRouteData(string FileName, System.Text.Encoding Encoding, ref RouteData Data, bool PreviewOnly)
		{
			string SignalPath, LimitPath, LimitGraphicsPath, TransponderPath;
			ObjectManager.StaticObject SignalPost, LimitPostStraight, LimitPostLeft, LimitPostRight, LimitPostInfinite;
			ObjectManager.StaticObject LimitOneDigit, LimitTwoDigits, LimitThreeDigits, StopPost;
			if (!PreviewOnly)
			{
				string CompatibilityFolder = Program.FileSystem.GetDataFolder("Compatibility");
				// load compatibility objects
				SignalPath = OpenBveApi.Path.CombineDirectory(CompatibilityFolder, "Signals");
				SignalPost = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(SignalPath, "signal_post.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitPath = OpenBveApi.Path.CombineDirectory(CompatibilityFolder, "Limits");
				LimitGraphicsPath = OpenBveApi.Path.CombineDirectory(LimitPath, "Graphics");
				LimitPostStraight = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_straight.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitPostLeft = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_left.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitPostRight = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_right.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitPostInfinite = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_infinite.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitOneDigit = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_1_digit.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitTwoDigits = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_2_digits.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);
				LimitThreeDigits = ObjectManager.LoadStaticObject(OpenBveApi.Path.CombineFile(LimitPath, "limit_3_digits.csv"), Encoding, ObjectManager.ObjectLoadMode.Normal, false, false, false);

			}
			else
			{
				SignalPath = null;
				LimitPath = null;
				LimitGraphicsPath = null;
				TransponderPath = null;
				SignalPost = null;
				LimitPostStraight = null;
				LimitPostLeft = null;
				LimitPostRight = null;
				LimitPostInfinite = null;
				LimitOneDigit = null;
				LimitTwoDigits = null;
				LimitThreeDigits = null;
				StopPost = null;
			}
			// initialize
			System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.InvariantCulture;
			int LastBlock = (int)Math.Floor((Data.TrackPosition + 600.0) / Data.BlockInterval + 0.001) + 1;
			int BlocksUsed = Data.Blocks.Length;
			CreateMissingBlocks(ref Data, ref BlocksUsed, LastBlock, PreviewOnly);
			Array.Resize<Block>(ref Data.Blocks, BlocksUsed);
			// interpolate height
			if (!PreviewOnly)
			{
				int z = 0;
				for (int i = 0; i < Data.Blocks.Length; i++)
				{
					if (!double.IsNaN(Data.Blocks[i].Height))
					{
						for (int j = i - 1; j >= 0; j--)
						{
							if (!double.IsNaN(Data.Blocks[j].Height))
							{
								double a = Data.Blocks[j].Height;
								double b = Data.Blocks[i].Height;
								double d = (b - a) / (double)(i - j);
								for (int k = j + 1; k < i; k++)
								{
									a += d;
									Data.Blocks[k].Height = a;
								}
								break;
							}
						}
						z = i;
					}
				}
				for (int i = z + 1; i < Data.Blocks.Length; i++)
				{
					Data.Blocks[i].Height = Data.Blocks[z].Height;
				}
			}
			// background
			if (!PreviewOnly)
			{
				BackgroundManager.CurrentBackground = new BackgroundManager.StaticBackground(null, 6, false);
				BackgroundManager.TargetBackground = BackgroundManager.CurrentBackground;
			}
			// brightness
			int CurrentBrightnessElement = -1;
			int CurrentBrightnessEvent = -1;
			float CurrentBrightnessValue = 1.0f;
			double CurrentBrightnessTrackPosition = (double)Data.FirstUsedBlock * Data.BlockInterval;
			if (!PreviewOnly)
			{
				for (int i = Data.FirstUsedBlock; i < Data.Blocks.Length; i++)
				{
					if (Data.Blocks[i].Brightness != null && Data.Blocks[i].Brightness.Length != 0)
					{
						CurrentBrightnessValue = Data.Blocks[i].Brightness[0].Value;
						CurrentBrightnessTrackPosition = Data.Blocks[i].Brightness[0].Value;
						break;
					}
				}
			}
			// create objects and track
			Vector3 Position = new Vector3(0.0, 0.0, 0.0);
			Vector2 Direction = new Vector2(0.0, 1.0);
			TrackManager.CurrentTrack = new TrackManager.Track { Elements = new TrackManager.TrackElement[] { } };
			double CurrentSpeedLimit = double.PositiveInfinity;
			int CurrentRunIndex = 0;
			int CurrentFlangeIndex = 0;
			if (Data.FirstUsedBlock < 0) Data.FirstUsedBlock = 0;
			TrackManager.CurrentTrack.Elements = new TrackManager.TrackElement[256];
			int CurrentTrackLength = 0;
			int PreviousFogElement = -1;
			int PreviousFogEvent = -1;
			Game.Fog PreviousFog = new Game.Fog(Game.NoFogStart, Game.NoFogEnd, new Color24(128, 128, 128), -Data.BlockInterval);
			Game.Fog CurrentFog = new Game.Fog(Game.NoFogStart, Game.NoFogEnd, new Color24(128, 128, 128), 0.0);
			// process blocks
			double progressFactor = Data.Blocks.Length - Data.FirstUsedBlock == 0 ? 0.5 : 0.5 / (double)(Data.Blocks.Length - Data.FirstUsedBlock);
			for (int i = Data.FirstUsedBlock; i < Data.Blocks.Length; i++)
			{
				Loading.RouteProgress = 0.6667 + (double)(i - Data.FirstUsedBlock) * progressFactor;
				if ((i & 15) == 0)
				{
					System.Threading.Thread.Sleep(1);
					if (Loading.Cancel) return;
				}
				double StartingDistance = (double)i * Data.BlockInterval;
				double EndingDistance = StartingDistance + Data.BlockInterval;
				// normalize
				World.Normalize(ref Direction.X, ref Direction.Y);
				// track
				if (!PreviewOnly)
				{
					if (Data.Blocks[i].Cycle.Length == 1 && Data.Blocks[i].Cycle[0] == -1)
					{
						if (Data.Structure.Cycle.Length == 0 || Data.Structure.Cycle[0] == null)
						{
							Data.Blocks[i].Cycle = new int[] { 0 };
						}
						else
						{
							Data.Blocks[i].Cycle = Data.Structure.Cycle[0];
						}
					}
				}
				TrackManager.TrackElement WorldTrackElement = Data.Blocks[i].CurrentTrackState;
				int n = CurrentTrackLength;
				if (n >= TrackManager.CurrentTrack.Elements.Length)
				{
					Array.Resize<TrackManager.TrackElement>(ref TrackManager.CurrentTrack.Elements, TrackManager.CurrentTrack.Elements.Length << 1);
				}
				CurrentTrackLength++;
				TrackManager.CurrentTrack.Elements[n] = WorldTrackElement;
				TrackManager.CurrentTrack.Elements[n].WorldPosition = Position;
				TrackManager.CurrentTrack.Elements[n].WorldDirection = Vector3.GetVector3(Direction, Data.Blocks[i].Pitch);
				TrackManager.CurrentTrack.Elements[n].WorldSide = new Vector3(Direction.Y, 0.0, -Direction.X);
				World.Cross(TrackManager.CurrentTrack.Elements[n].WorldDirection.X, TrackManager.CurrentTrack.Elements[n].WorldDirection.Y, TrackManager.CurrentTrack.Elements[n].WorldDirection.Z, TrackManager.CurrentTrack.Elements[n].WorldSide.X, TrackManager.CurrentTrack.Elements[n].WorldSide.Y, TrackManager.CurrentTrack.Elements[n].WorldSide.Z, out TrackManager.CurrentTrack.Elements[n].WorldUp.X, out TrackManager.CurrentTrack.Elements[n].WorldUp.Y, out TrackManager.CurrentTrack.Elements[n].WorldUp.Z);
				TrackManager.CurrentTrack.Elements[n].StartingTrackPosition = StartingDistance;
				TrackManager.CurrentTrack.Elements[n].Events = new TrackManager.GeneralEvent[] { };
				TrackManager.CurrentTrack.Elements[n].AdhesionMultiplier = Data.Blocks[i].AdhesionMultiplier;
				TrackManager.CurrentTrack.Elements[n].CsvRwAccuracyLevel = Data.Blocks[i].Accuracy;
				// background
				if (!PreviewOnly)
				{
					if (Data.Blocks[i].Background >= 0)
					{
						int typ;
						if (i == Data.FirstUsedBlock)
						{
							typ = Data.Blocks[i].Background;
						}
						else
						{
							typ = Data.Backgrounds.Length > 0 ? 0 : -1;
							for (int j = i - 1; j >= Data.FirstUsedBlock; j--)
							{
								if (Data.Blocks[j].Background >= 0)
								{
									typ = Data.Blocks[j].Background;
									break;
								}
							}
						}
						if (typ >= 0 & typ < Data.Backgrounds.Length)
						{
							int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
							Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
							TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.BackgroundChangeEvent(0.0, Data.Backgrounds[typ].Handle, Data.Backgrounds[Data.Blocks[i].Background].Handle);
						}
					}
				}
				// brightness
				if (!PreviewOnly)
				{
					for (int j = 0; j < Data.Blocks[i].Brightness.Length; j++)
					{
						int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
						Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
						double d = Data.Blocks[i].Brightness[j].TrackPosition - StartingDistance;
						TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.BrightnessChangeEvent(d, Data.Blocks[i].Brightness[j].Value, CurrentBrightnessValue, Data.Blocks[i].Brightness[j].TrackPosition - CurrentBrightnessTrackPosition);
						if (CurrentBrightnessElement >= 0 & CurrentBrightnessEvent >= 0)
						{
							TrackManager.BrightnessChangeEvent bce = (TrackManager.BrightnessChangeEvent)TrackManager.CurrentTrack.Elements[CurrentBrightnessElement].Events[CurrentBrightnessEvent];
							bce.NextBrightness = Data.Blocks[i].Brightness[j].Value;
							bce.NextDistance = Data.Blocks[i].Brightness[j].TrackPosition - CurrentBrightnessTrackPosition;
						}
						CurrentBrightnessElement = n;
						CurrentBrightnessEvent = m;
						CurrentBrightnessValue = Data.Blocks[i].Brightness[j].Value;
						CurrentBrightnessTrackPosition = Data.Blocks[i].Brightness[j].TrackPosition;
					}
				}
				// fog
				if (!PreviewOnly)
				{
					if (Data.FogTransitionMode)
					{
						if (Data.Blocks[i].FogDefined)
						{
							Data.Blocks[i].Fog.TrackPosition = StartingDistance;
							int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
							Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
							TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.FogChangeEvent(0.0, PreviousFog, Data.Blocks[i].Fog, Data.Blocks[i].Fog);
							if (PreviousFogElement >= 0 & PreviousFogEvent >= 0)
							{
								TrackManager.FogChangeEvent e = (TrackManager.FogChangeEvent)TrackManager.CurrentTrack.Elements[PreviousFogElement].Events[PreviousFogEvent];
								e.NextFog = Data.Blocks[i].Fog;
							}
							else
							{
								Game.PreviousFog = PreviousFog;
								Game.CurrentFog = PreviousFog;
								Game.NextFog = Data.Blocks[i].Fog;
							}
							PreviousFog = Data.Blocks[i].Fog;
							PreviousFogElement = n;
							PreviousFogEvent = m;
						}
					}
					else
					{
						Data.Blocks[i].Fog.TrackPosition = StartingDistance + Data.BlockInterval;
						int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
						Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
						TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.FogChangeEvent(0.0, PreviousFog, CurrentFog, Data.Blocks[i].Fog);
						PreviousFog = CurrentFog;
						CurrentFog = Data.Blocks[i].Fog;
					}
				}
				// rail sounds
				if (!PreviewOnly)
				{
					for (int k = 0; k < Data.Blocks[i].RunSounds.Length; k++)
					{
						//Get & check our variables
						int r = Data.Blocks[i].RunSounds[k].RunSoundIndex;
						int f = Data.Blocks[i].RunSounds[k].FlangeSoundIndex;
						if (r != CurrentRunIndex || f != CurrentFlangeIndex)
						{
							//If either of these differ, we first need to resize the array for the current block
							int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
							Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
							double d = Data.Blocks[i].RunSounds[k].TrackPosition - StartingDistance;
							if (d > 0.0)
							{
								d = 0.0;
							}
							//Add event
							TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.RailSoundsChangeEvent(d, CurrentRunIndex, CurrentFlangeIndex, r, f);
							CurrentRunIndex = r;
							CurrentFlangeIndex = f;
						}
					}
				}
				// point sound
				if (!PreviewOnly)
				{
					if (i < Data.Blocks.Length - 1)
					{
						if (Data.Blocks[i].JointNoise)
						{
							int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
							Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
							TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.SoundEvent(0.0, null, false, false, true, new Vector3(0.0, 0.0, 0.0), 12.5);
						}
					}
				}
				// station
				if (Data.Blocks[i].Station >= 0)
				{
					// station
					int s = Data.Blocks[i].Station;
					int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
					Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
					TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.StationStartEvent(0.0, s);
					double dx, dy = 3.0;
					if (Game.Stations[s].OpenLeftDoors & !Game.Stations[s].OpenRightDoors)
					{
						dx = -5.0;
					}
					else if (!Game.Stations[s].OpenLeftDoors & Game.Stations[s].OpenRightDoors)
					{
						dx = 5.0;
					}
					else
					{
						dx = 0.0;
					}
					Game.Stations[s].SoundOrigin.X = Position.X + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.X + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.X;
					Game.Stations[s].SoundOrigin.Y = Position.Y + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.Y + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.Y;
					Game.Stations[s].SoundOrigin.Z = Position.Z + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.Z + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.Z;
					// passalarm
					if (!PreviewOnly)
					{
						if (Data.Blocks[i].StationPassAlarm)
						{
							int b = i - 6;
							if (b >= 0)
							{
								int j = b - Data.FirstUsedBlock;
								if (j >= 0)
								{
									m = TrackManager.CurrentTrack.Elements[j].Events.Length;
									Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[j].Events, m + 1);
									TrackManager.CurrentTrack.Elements[j].Events[m] = new TrackManager.StationPassAlarmEvent(0.0);
								}
							}
						}
					}
				}
				// stop
				for (int j = 0; j < Data.Blocks[i].Stop.Length; j++)
				{
					int s = Data.Blocks[i].Stop[j].Station;
					int t = Game.Stations[s].Stops.Length;
					Array.Resize<Game.StationStop>(ref Game.Stations[s].Stops, t + 1);
					Game.Stations[s].Stops[t].TrackPosition = Data.Blocks[i].Stop[j].TrackPosition;
					Game.Stations[s].Stops[t].ForwardTolerance = Data.Blocks[i].Stop[j].ForwardTolerance;
					Game.Stations[s].Stops[t].BackwardTolerance = Data.Blocks[i].Stop[j].BackwardTolerance;
					Game.Stations[s].Stops[t].Cars = Data.Blocks[i].Stop[j].Cars;
					double dx, dy = 2.0;
					if (Game.Stations[s].OpenLeftDoors & !Game.Stations[s].OpenRightDoors)
					{
						dx = -5.0;
					}
					else if (!Game.Stations[s].OpenLeftDoors & Game.Stations[s].OpenRightDoors)
					{
						dx = 5.0;
					}
					else
					{
						dx = 0.0;
					}
					Game.Stations[s].SoundOrigin.X = Position.X + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.X + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.X;
					Game.Stations[s].SoundOrigin.Y = Position.Y + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.Y + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.Y;
					Game.Stations[s].SoundOrigin.Z = Position.Z + dx * TrackManager.CurrentTrack.Elements[n].WorldSide.Z + dy * TrackManager.CurrentTrack.Elements[n].WorldUp.Z;
				}
				// limit
				for (int j = 0; j < Data.Blocks[i].Limit.Length; j++)
				{
					int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
					Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
					double d = Data.Blocks[i].Limit[j].TrackPosition - StartingDistance;
					TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.LimitChangeEvent(d, CurrentSpeedLimit, Data.Blocks[i].Limit[j].Speed);
					CurrentSpeedLimit = Data.Blocks[i].Limit[j].Speed;
				}
				// turn
				if (Data.Blocks[i].Turn != 0.0)
				{
					double ag = -Math.Atan(Data.Blocks[i].Turn);
					double cosag = Math.Cos(ag);
					double sinag = Math.Sin(ag);
					World.Rotate(ref Direction, cosag, sinag);
					World.RotatePlane(ref TrackManager.CurrentTrack.Elements[n].WorldDirection, cosag, sinag);
					World.RotatePlane(ref TrackManager.CurrentTrack.Elements[n].WorldSide, cosag, sinag);
					World.Cross(TrackManager.CurrentTrack.Elements[n].WorldDirection.X, TrackManager.CurrentTrack.Elements[n].WorldDirection.Y, TrackManager.CurrentTrack.Elements[n].WorldDirection.Z, TrackManager.CurrentTrack.Elements[n].WorldSide.X, TrackManager.CurrentTrack.Elements[n].WorldSide.Y, TrackManager.CurrentTrack.Elements[n].WorldSide.Z, out TrackManager.CurrentTrack.Elements[n].WorldUp.X, out TrackManager.CurrentTrack.Elements[n].WorldUp.Y, out TrackManager.CurrentTrack.Elements[n].WorldUp.Z);
				}
				//Pitch
				if (Data.Blocks[i].Pitch != 0.0)
				{
					TrackManager.CurrentTrack.Elements[n].Pitch = Data.Blocks[i].Pitch;
				}
				else
				{
					TrackManager.CurrentTrack.Elements[n].Pitch = 0.0;
				}
				// curves
				double a = 0.0;
				double c = Data.BlockInterval;
				double h = 0.0;
				if (WorldTrackElement.CurveRadius != 0.0 & Data.Blocks[i].Pitch != 0.0)
				{
					double d = Data.BlockInterval;
					double p = Data.Blocks[i].Pitch;
					double r = WorldTrackElement.CurveRadius;
					double s = d / Math.Sqrt(1.0 + p * p);
					h = s * p;
					double b = s / Math.Abs(r);
					c = Math.Sqrt(2.0 * r * r * (1.0 - Math.Cos(b)));
					a = 0.5 * (double)Math.Sign(r) * b;
					World.Rotate(ref Direction, Math.Cos(-a), Math.Sin(-a));
				}
				else if (WorldTrackElement.CurveRadius != 0.0)
				{
					double d = Data.BlockInterval;
					double r = WorldTrackElement.CurveRadius;
					double b = d / Math.Abs(r);
					c = Math.Sqrt(2.0 * r * r * (1.0 - Math.Cos(b)));
					a = 0.5 * (double)Math.Sign(r) * b;
					World.Rotate(ref Direction, Math.Cos(-a), Math.Sin(-a));
				}
				else if (Data.Blocks[i].Pitch != 0.0)
				{
					double p = Data.Blocks[i].Pitch;
					double d = Data.BlockInterval;
					c = d / Math.Sqrt(1.0 + p * p);
					h = c * p;
				}
				double TrackYaw = Math.Atan2(Direction.X, Direction.Y);
				double TrackPitch = Math.Atan(Data.Blocks[i].Pitch);
				World.Transformation GroundTransformation = new World.Transformation(TrackYaw, 0.0, 0.0);
				World.Transformation TrackGroundTransformation = new World.Transformation(TrackYaw, 0.0, 0.0);
				World.Transformation TrackTransformation = new World.Transformation(TrackYaw, TrackPitch, 0.0);
				World.Transformation NullTransformation = new World.Transformation(0.0, 0.0, 0.0);
				//Add repeating objects into arrays
				if (!PreviewOnly)
				{
					if (Data.Blocks[i].Repeaters == null || Data.Blocks[i].Repeaters.Length == 0)
					{
						continue;
					}
					for (int j = 0; j < Data.Blocks[i].Repeaters.Length; j++)
					{
						if (Data.Blocks[i].Repeaters[j].Name == null)
						{
							continue;
						}
						switch (Data.Blocks[i].Repeaters[j].Type)
						{
							case 0:
								if (Data.Blocks[i].Repeaters[j].RepetitionInterval != 0.0 && Data.Blocks[i].Repeaters[j].RepetitionInterval != Data.BlockInterval)
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									double nextRepetition = Data.Blocks[i].Repeaters[j].TrackPosition;
									//If the repetition interval is not 0.0 then this may repeat within a block
									if (i > 0)
									{
										for (int k = 0; k < Data.Blocks[i - 1].Repeaters.Length; k++)
										{
											if (Data.Blocks[i - 1].Repeaters[k].Name == Data.Blocks[i].Repeaters[j].Name)
											{
												//We've found our repeater in the last block, so we must add the repetition interval
												nextRepetition = Data.Blocks[i - 1].Repeaters[k].TrackPosition +
																 Data.Blocks[i - 1].Repeaters[k].RepetitionInterval;

												int ol = 0;
												if (Data.Blocks[i].RailFreeObj[idx] == null)
												{
													Data.Blocks[i].RailFreeObj[idx] = new Object[1];
													ol = 0;
												}
												else
												{
													ol = Data.Blocks[i].RailFreeObj[idx].Length;
													Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
												}
												Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
												Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
												Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
												Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
												Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
												Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
												Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
												Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
												Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.Flat;
											}
										}
									}
									while (nextRepetition < Data.Blocks[i].Repeaters[j].TrackPosition + Data.BlockInterval)
									{
										int ol = 0;
										if (Data.Blocks[i].RailFreeObj[idx] == null)
										{
											Data.Blocks[i].RailFreeObj[idx] = new Object[1];
											ol = 0;
										}
										else
										{
											ol = Data.Blocks[i].RailFreeObj[idx].Length;
											Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
										}
										nextRepetition += Data.Blocks[i].Repeaters[j].RepetitionInterval;
										Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
										Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
										Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
										Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
										Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
										Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
										Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
										Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
										Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.Flat;
									}
									Data.Blocks[i].Repeaters[j].TrackPosition = nextRepetition;

								}
								else
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									int ol = 0;
									if (Data.Blocks[i].RailFreeObj[idx] == null)
									{
										Data.Blocks[i].RailFreeObj[idx] = new Object[1];
										ol = 0;
									}
									else
									{
										ol = Data.Blocks[i].RailFreeObj[idx].Length;
										Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
									}
									Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = Data.Blocks[i].Repeaters[j].TrackPosition;
									Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
									Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
									Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
									Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
									Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
									Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
									Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
									Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.Flat;
								}
								break;
							case 1:
								//The repeater follows the gradient of it's attached rail, or Rail0 if not specified, so we must add it to the rail's object array
								if (Data.Blocks[i].Repeaters[j].RepetitionInterval != 0.0 && Data.Blocks[i].Repeaters[j].RepetitionInterval != Data.BlockInterval)
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									double nextRepetition = Data.Blocks[i].Repeaters[j].TrackPosition;
									//If the repetition interval is not 0.0 then this may repeat within a block
									if (i > 0)
									{
										for (int k = 0; k < Data.Blocks[i - 1].Repeaters.Length; k++)
										{
											if (Data.Blocks[i - 1].Repeaters[k].Name == Data.Blocks[i].Repeaters[j].Name)
											{
												//We've found our repeater in the last block, so we must add the repetition interval
												nextRepetition = Data.Blocks[i - 1].Repeaters[k].TrackPosition +
																 Data.Blocks[i - 1].Repeaters[k].RepetitionInterval;

												int ol = 0;
												if (Data.Blocks[i].RailFreeObj[idx] == null)
												{
													Data.Blocks[i].RailFreeObj[idx] = new Object[1];
													ol = 0;
												}
												else
												{
													ol = Data.Blocks[i].RailFreeObj[idx].Length;
													Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
												}
												Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
												Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
												Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
												Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
												Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
												Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
												Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
												Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
												Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsPitch;
											}
										}
									}
									while (nextRepetition < Data.Blocks[i].Repeaters[j].TrackPosition + Data.BlockInterval)
									{
										int ol = 0;
										if (Data.Blocks[i].RailFreeObj[idx] == null)
										{
											Data.Blocks[i].RailFreeObj[idx] = new Object[1];
											ol = 0;
										}
										else
										{
											ol = Data.Blocks[i].RailFreeObj[idx].Length;
											Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
										}
										nextRepetition += Data.Blocks[i].Repeaters[j].RepetitionInterval;
										Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
										Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
										Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
										Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
										Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
										Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
										Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
										Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
										Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsPitch;
									}
									Data.Blocks[i].Repeaters[j].TrackPosition = nextRepetition;

								}
								else
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									int ol = 0;
									if (Data.Blocks[i].RailFreeObj[idx] == null)
									{
										Data.Blocks[i].RailFreeObj[idx] = new Object[1];
										ol = 0;
									}
									else
									{
										ol = Data.Blocks[i].RailFreeObj[idx].Length;
										Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
									}
									Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = Data.Blocks[i].Repeaters[j].TrackPosition;
									Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
									Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
									Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
									Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
									Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
									Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
									Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
									Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsPitch;
								}
								break;
							case 2:
								//The repeater follows the cant of it's attached rail, or Rail0 if not specified, so we must add it to the rail's object array
								if (Data.Blocks[i].Repeaters[j].RepetitionInterval != 0.0 && Data.Blocks[i].Repeaters[j].RepetitionInterval != Data.BlockInterval)
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									double nextRepetition = Data.Blocks[i].Repeaters[j].TrackPosition;
									//If the repetition interval is not 0.0 then this may repeat within a block
									if (i > 0)
									{
										for (int k = 0; k < Data.Blocks[i - 1].Repeaters.Length; k++)
										{
											if (Data.Blocks[i - 1].Repeaters[k].Name == Data.Blocks[i].Repeaters[j].Name)
											{
												//We've found our repeater in the last block, so we must add the repetition interval
												nextRepetition = Data.Blocks[i - 1].Repeaters[k].TrackPosition +
																 Data.Blocks[i - 1].Repeaters[k].RepetitionInterval;

												int ol = 0;
												if (Data.Blocks[i].RailFreeObj[idx] == null)
												{
													Data.Blocks[i].RailFreeObj[idx] = new Object[1];
													ol = 0;
												}
												else
												{
													ol = Data.Blocks[i].RailFreeObj[idx].Length;
													Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
												}
												Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
												Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
												Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
												Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
												Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
												Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
												Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
												Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
												Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsCant;
											}
										}
									}
									while (nextRepetition < Data.Blocks[i].Repeaters[j].TrackPosition + Data.BlockInterval)
									{
										int ol = 0;
										if (Data.Blocks[i].RailFreeObj[idx] == null)
										{
											Data.Blocks[i].RailFreeObj[idx] = new Object[1];
											ol = 0;
										}
										else
										{
											ol = Data.Blocks[i].RailFreeObj[idx].Length;
											Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
										}
										nextRepetition += Data.Blocks[i].Repeaters[j].RepetitionInterval;
										Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
										Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
										Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
										Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
										Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
										Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
										Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
										Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
										Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsCant;
									}
									Data.Blocks[i].Repeaters[j].TrackPosition = nextRepetition;

								}
								else
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									int ol = 0;
									if (Data.Blocks[i].RailFreeObj[idx] == null)
									{
										Data.Blocks[i].RailFreeObj[idx] = new Object[1];
										ol = 0;
									}
									else
									{
										ol = Data.Blocks[i].RailFreeObj[idx].Length;
										Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
									}
									Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = Data.Blocks[i].Repeaters[j].TrackPosition;
									Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
									Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
									Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
									Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
									Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
									Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
									Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll;
									Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsCant;
								}
								break;
							case 3:
								//The repeater follows the gradient & cant of it's attached rail, or Rail0 if not specified, so we must add it to the rail's object array
								double CantAngle = Math.Tan((Math.Atan(Data.Blocks[i].CurrentTrackState.CurveCant)));
								if (Data.Blocks[i].Repeaters[j].RepetitionInterval != 0.0)
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									double nextRepetition = Data.Blocks[i].Repeaters[j].TrackPosition;

									//If the repetition interval is not 0.0 then this may repeat within a block
									if (i > 0)
									{
										for (int k = 0; k < Data.Blocks[i - 1].Repeaters.Length; k++)
										{
											if (Data.Blocks[i - 1].Repeaters[k].Name == Data.Blocks[i].Repeaters[j].Name)
											{
												//We've found our repeater in the last block, so we must add the repetition interval
												nextRepetition = Data.Blocks[i - 1].Repeaters[k].TrackPosition +
																 Data.Blocks[i - 1].Repeaters[k].RepetitionInterval;

												int ol = 0;
												if (Data.Blocks[i].RailFreeObj[idx] == null)
												{
													Data.Blocks[i].RailFreeObj[idx] = new Object[1];
													ol = 0;
												}
												else
												{
													ol = Data.Blocks[i].RailFreeObj[idx].Length;
													Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
												}
												Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
												Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
												Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
												Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
												Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
												Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
												Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
												Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll + CantAngle;
												Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsBoth;
											}
										}
									}
									while (nextRepetition < Data.Blocks[i].Repeaters[j].TrackPosition + Data.BlockInterval)
									{
										int ol = 0;
										if (Data.Blocks[i].RailFreeObj[idx] == null)
										{
											Data.Blocks[i].RailFreeObj[idx] = new Object[1];
											ol = 0;
										}
										else
										{
											ol = Data.Blocks[i].RailFreeObj[idx].Length;
											Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
										}
										nextRepetition += Data.Blocks[i].Repeaters[j].RepetitionInterval;
										Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = nextRepetition;
										Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
										Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
										Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
										Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
										Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
										Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
										Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll + CantAngle;
										Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsBoth;
									}
									Data.Blocks[i].Repeaters[j].TrackPosition = nextRepetition;

								}
								else
								{
									int idx = Data.Blocks[i].Repeaters[j].RailIndex;
									if (idx >= Data.Blocks[i].RailFreeObj.Length)
									{
										Array.Resize<Object[]>(ref Data.Blocks[i].RailFreeObj, idx + 1);
									}
									int ol = 0;
									if (Data.Blocks[i].RailFreeObj[idx] == null)
									{
										Data.Blocks[i].RailFreeObj[idx] = new Object[1];
										ol = 0;
									}
									else
									{
										ol = Data.Blocks[i].RailFreeObj[idx].Length;
										Array.Resize<Object>(ref Data.Blocks[i].RailFreeObj[idx], ol + 1);
									}
									Data.Blocks[i].RailFreeObj[idx][ol].TrackPosition = Data.Blocks[i].Repeaters[j].TrackPosition;
									Data.Blocks[i].RailFreeObj[idx][ol].Type = Data.Blocks[i].Repeaters[j].StructureTypes[0];
									Data.Blocks[i].RailFreeObj[idx][ol].X = Data.Blocks[i].Repeaters[j].X;
									Data.Blocks[i].RailFreeObj[idx][ol].Y = Data.Blocks[i].Repeaters[j].Y;
									Data.Blocks[i].RailFreeObj[idx][ol].Z = Data.Blocks[i].Repeaters[j].Z;
									Data.Blocks[i].RailFreeObj[idx][ol].Yaw = Data.Blocks[i].Repeaters[j].Yaw;
									Data.Blocks[i].RailFreeObj[idx][ol].Pitch = Data.Blocks[i].Repeaters[j].Pitch;
									Data.Blocks[i].RailFreeObj[idx][ol].Roll = Data.Blocks[i].Repeaters[j].Roll + CantAngle;
									Data.Blocks[i].RailFreeObj[idx][ol].BaseTransformation = RailTransformationTypes.FollowsBoth;
								}
								break;
						}
					}
				}
				// ground-aligned free objects
				if (!PreviewOnly)
				{
					for (int j = 0; j < Data.Blocks[i].GroundFreeObj.Length; j++)
					{
						int sttype = Data.Blocks[i].GroundFreeObj[j].Type;
						double d = Data.Blocks[i].GroundFreeObj[j].TrackPosition - StartingDistance + Data.Blocks[i].GroundFreeObj[j].Z;
						double dx = Data.Blocks[i].GroundFreeObj[j].X;
						double dy = Data.Blocks[i].GroundFreeObj[j].Y;
						Vector3 wpos = Position + new Vector3(Direction.X * d + Direction.Y * dx, dy - Data.Blocks[i].Height, Direction.Y * d - Direction.X * dx);
						double tpos = Data.Blocks[i].GroundFreeObj[j].TrackPosition;
						if (sttype > Data.Structure.Objects.Length || Data.Structure.Objects[sttype] == null)
						{
							continue;
						}
						Data.Structure.Objects[sttype].CreateObject(wpos, GroundTransformation, new World.Transformation(Data.Blocks[i].GroundFreeObj[j].Yaw, Data.Blocks[i].GroundFreeObj[j].Pitch, Data.Blocks[i].GroundFreeObj[j].Roll), Data.AccurateObjectDisposal, StartingDistance, EndingDistance, Data.BlockInterval, tpos);
					}
				}
				// rail-aligned objects
				if (!PreviewOnly)
				{
					for (int j = 0; j < Data.Blocks[i].Rail.Length; j++)
					{
						if (j > 0 && !Data.Blocks[i].Rail[j].RailStart) continue;
						// rail
						Vector3 pos;
						World.Transformation RailTransformation;
						World.Transformation RailNullTransformation;
						double planar, updown;
						if (j == 0)
						{
							// rail 0
							planar = 0.0;
							updown = 0.0;
							RailTransformation = new World.Transformation(TrackTransformation, planar, updown, 0.0);
							RailNullTransformation = new World.Transformation(RailTransformation);
							RailNullTransformation.Y = new Vector3(0, 1.0, 0);
							pos = Position;
						}
						else
						{
							// rails 1-infinity
							double x = Data.Blocks[i].Rail[j].RailStartX;
							double y = Data.Blocks[i].Rail[j].RailStartY;
							Vector3 offset = new Vector3(Direction.Y * x, y, -Direction.X * x);
							pos = Position + offset;
							double dh;
							if (i < Data.Blocks.Length - 1 && Data.Blocks[i + 1].Rail.Length > j)
							{
								// take orientation of upcoming block into account
								Vector2 Direction2 = Direction;
								Vector3 Position2 = Position;
								Position2.X += Direction.X * c;
								Position2.Y += h;
								Position2.Z += Direction.Y * c;
								if (a != 0.0)
								{
									World.Rotate(ref Direction2, Math.Cos(-a), Math.Sin(-a));
								}
								if (Data.Blocks[i + 1].Turn != 0.0)
								{
									double ag = -Math.Atan(Data.Blocks[i + 1].Turn);
									double cosag = Math.Cos(ag);
									double sinag = Math.Sin(ag);
									World.Rotate(ref Direction2, cosag, sinag);
								}
								double a2 = 0.0;
								double c2 = Data.BlockInterval;
								double h2 = 0.0;
								if (Data.Blocks[i + 1].CurrentTrackState.CurveRadius != 0.0 & Data.Blocks[i + 1].Pitch != 0.0)
								{
									double d2 = Data.BlockInterval;
									double p2 = Data.Blocks[i + 1].Pitch;
									double r2 = Data.Blocks[i + 1].CurrentTrackState.CurveRadius;
									double s2 = d2 / Math.Sqrt(1.0 + p2 * p2);
									h2 = s2 * p2;
									double b2 = s2 / Math.Abs(r2);
									c2 = Math.Sqrt(2.0 * r2 * r2 * (1.0 - Math.Cos(b2)));
									a2 = 0.5 * (double)Math.Sign(r2) * b2;
									World.Rotate(ref Direction2, Math.Cos(-a2), Math.Sin(-a2));
								}
								else if (Data.Blocks[i + 1].CurrentTrackState.CurveRadius != 0.0)
								{
									double d2 = Data.BlockInterval;
									double r2 = Data.Blocks[i + 1].CurrentTrackState.CurveRadius;
									double b2 = d2 / Math.Abs(r2);
									c2 = Math.Sqrt(2.0 * r2 * r2 * (1.0 - Math.Cos(b2)));
									a2 = 0.5 * (double)Math.Sign(r2) * b2;
									World.Rotate(ref Direction2, Math.Cos(-a2), Math.Sin(-a2));
								}
								else if (Data.Blocks[i + 1].Pitch != 0.0)
								{
									double p2 = Data.Blocks[i + 1].Pitch;
									double d2 = Data.BlockInterval;
									c2 = d2 / Math.Sqrt(1.0 + p2 * p2);
									h2 = c2 * p2;
								}
								double TrackYaw2 = Math.Atan2(Direction2.X, Direction2.Y);
								double TrackPitch2 = Math.Atan(Data.Blocks[i + 1].Pitch);
								World.Transformation GroundTransformation2 = new World.Transformation(TrackYaw2, 0.0, 0.0);
								World.Transformation TrackTransformation2 = new World.Transformation(TrackYaw2, TrackPitch2, 0.0);
								RailTransformation = new World.Transformation(TrackTransformation2, 0.0, 0.0, 0.0);
								TrackGroundTransformation = new World.Transformation(GroundTransformation2, 0.0, 0.0, 0.0);
								double x2 = Data.Blocks[i + 1].Rail[j].RailEndX;
								double y2 = Data.Blocks[i + 1].Rail[j].RailEndY;
								Vector3 offset2 = new Vector3(Direction2.Y * x2, y2, -Direction2.X * x2);

								/*
								 * Create transform for rails
								 */
								Vector3 pos2 = Position2 + offset2;
								double rx = pos2.X - pos.X;
								double ry = pos2.Y - pos.Y;
								double rz = pos2.Z - pos.Z;
								World.Normalize(ref rx, ref ry, ref rz);
								RailTransformation.Z = new Vector3(rx, ry, rz);
								RailTransformation.X = new Vector3(rz, 0.0, -rx);
								World.Normalize(ref RailTransformation.X.X, ref RailTransformation.X.Z);
								RailTransformation.Y = Vector3.Cross(RailTransformation.Z, RailTransformation.X);
								/*
								 * Create transform for rail attached grounds
								 */
								Vector3 offset3 = new Vector3(Direction2.Y * x2, 0.0, -Direction2.X * x2);
								Vector3 pos3 = Position2 + offset3;
								rx = pos3.X;
								ry = pos3.Y;
								rz = pos3.Z;
								World.Normalize(ref rx, ref ry, ref rz);
								TrackGroundTransformation.Z = new Vector3(rx, ry, rz);
								TrackGroundTransformation.X = new Vector3(rz, 0.0, -rx);
								World.Normalize(ref TrackGroundTransformation.X.X, ref TrackGroundTransformation.X.Z);
								TrackGroundTransformation.Y = Vector3.Cross(TrackGroundTransformation.Z, TrackGroundTransformation.X);


								double dx = Data.Blocks[i + 1].Rail[j].RailEndX - Data.Blocks[i].Rail[j].RailStartX;
								double dy = Data.Blocks[i + 1].Rail[j].RailEndY - Data.Blocks[i].Rail[j].RailStartY;
								planar = Math.Atan(dx / c);
								dh = dy / c;
								updown = Math.Atan(dh);
								RailNullTransformation = new World.Transformation(RailTransformation);
								RailNullTransformation.Y = new Vector3(0, 1.0, 0);

							}
							else
							{
								planar = 0.0;
								dh = 0.0;
								updown = 0.0;
								RailTransformation = new World.Transformation(TrackTransformation, 0.0, 0.0, 0.0);
								TrackGroundTransformation = new World.Transformation(GroundTransformation, 0.0, 0.0, 0.0);
								RailNullTransformation = new World.Transformation(RailTransformation);
								RailNullTransformation.Y = new Vector3(0, 1.0, 0);
							}
						}
						// cracks
						for (int k = 0; k < Data.Blocks[i].Crack.Length; k++)
						{
							if (Data.Blocks[i].Crack[k].PrimaryRail == j)
							{
								int p = Data.Blocks[i].Crack[k].PrimaryRail;
								double px0 = p > 0 ? Data.Blocks[i].Rail[p].RailStartX : 0.0;
								double px1 = p > 0 ? Data.Blocks[i + 1].Rail[p].RailEndX : 0.0;
								int s = Data.Blocks[i].Crack[k].SecondaryRail;
								if (s < 0 || s >= Data.Blocks[i].Rail.Length || !Data.Blocks[i].Rail[s].RailStart)
								{
									Interface.AddMessage(Interface.MessageType.Error, false, "RailIndex2 is out of range in Track.Crack at track position " + StartingDistance.ToString(Culture) + " in file " + FileName);
								}
								else
								{
									double sx0 = Data.Blocks[i].Rail[s].RailStartX;
									double sx1 = Data.Blocks[i + 1].Rail[s].RailEndX;
									double d0 = sx0 - px0;
									double d1 = sx1 - px1;
									if (d0 < 0.0)
									{
										if (Data.Blocks[i].Crack[k].Type >= Data.Structure.Objects.Length || Data.Structure.Objects[Data.Blocks[i].Crack[k].Type] == null)
										{
											Interface.AddMessage(Interface.MessageType.Error, false, "CrackStructureIndex references a CrackL not loaded in Track.Crack at track position " + StartingDistance.ToString(Culture) + " in file " + FileName + ".");
										}
										else
										{
											ObjectManager.StaticObject Crack = GetTransformedStaticObject((ObjectManager.StaticObject)Data.Structure.Objects[Data.Blocks[i].Crack[k].Type], d0, d1);
											ObjectManager.CreateStaticObject(Crack, pos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, StartingDistance, EndingDistance, Data.BlockInterval, StartingDistance);
										}
									}
									else if (d0 > 0.0)
									{
										if (Data.Blocks[i].Crack[k].Type >= Data.Structure.Objects.Length || Data.Structure.Objects[Data.Blocks[i].Crack[k].Type] == null)
										{
											Interface.AddMessage(Interface.MessageType.Error, false, "CrackStructureIndex references a CrackR not loaded in Track.Crack at track position " + StartingDistance.ToString(Culture) + " in file " + FileName + ".");
										}
										else
										{
											ObjectManager.StaticObject Crack = GetTransformedStaticObject((ObjectManager.StaticObject)Data.Structure.Objects[Data.Blocks[i].Crack[k].Type], d0, d1);
											ObjectManager.CreateStaticObject(Crack, pos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, StartingDistance, EndingDistance, Data.BlockInterval, StartingDistance);
										}
									}
								}
							}
						}
						// free objects
						if (Data.Blocks[i].RailFreeObj.Length > j && Data.Blocks[i].RailFreeObj[j] != null)
						{
							for (int k = 0; k < Data.Blocks[i].RailFreeObj[j].Length; k++)
							{
								int sttype = Data.Blocks[i].RailFreeObj[j][k].Type;
								if (sttype != -1)
								{
									double dx = Data.Blocks[i].RailFreeObj[j][k].X;
									double dy = Data.Blocks[i].RailFreeObj[j][k].Y;
									double dz = Data.Blocks[i].RailFreeObj[j][k].TrackPosition - StartingDistance + Data.Blocks[i].RailFreeObj[j][k].Z;

									Vector3 wpos = pos;
									wpos.X += dx * RailTransformation.X.X + dy * RailTransformation.Y.X + dz * RailTransformation.Z.X;
									wpos.Y += dx * RailTransformation.X.Y + dy * RailTransformation.Y.Y + dz * RailTransformation.Z.Y;
									wpos.Z += dx * RailTransformation.X.Z + dy * RailTransformation.Y.Z + dz * RailTransformation.Z.Z;
									double tpos = Data.Blocks[i].RailFreeObj[j][k].TrackPosition;
									if (sttype > Data.Structure.Objects.Length || Data.Structure.Objects[sttype] == null)
									{
										continue;
									}
									switch (Data.Blocks[i].RailFreeObj[j][k].BaseTransformation)
									{
										case RailTransformationTypes.Flat:
											//As expected, this is used for repeaters with a type value of zero
											//However, to get correct appearance in-game, they actually seem to require the rail transform....

											//TODO: Check how the transform is created; I presume we require a new one which doesn't take into account
											//height or cant, but does X & Y positions of the rail???
											//Sounds remarkably like the ground transform, but this doesn't appear to work....
											Data.Structure.Objects[sttype].CreateObject(wpos, RailNullTransformation,
												new World.Transformation(Data.Blocks[i].RailFreeObj[j][k].Yaw, Data.Blocks[i].RailFreeObj[j][k].Pitch,
													Data.Blocks[i].RailFreeObj[j][k].Roll), -1, Data.AccurateObjectDisposal, StartingDistance, EndingDistance,
												Data.BlockInterval, tpos, 1.0, false);
											break;
										case RailTransformationTypes.FollowsPitch:
											Data.Structure.Objects[sttype].CreateObject(wpos, RailTransformation,
												new World.Transformation(Data.Blocks[i].RailFreeObj[j][k].Yaw, Data.Blocks[i].RailFreeObj[j][k].Pitch,
													Data.Blocks[i].RailFreeObj[j][k].Roll), -1, Data.AccurateObjectDisposal, StartingDistance, EndingDistance,
												Data.BlockInterval, tpos, 1.0, false);
											break;
										case RailTransformationTypes.FollowsCant:
											Data.Structure.Objects[sttype].CreateObject(wpos, TrackGroundTransformation,
												new World.Transformation(Data.Blocks[i].RailFreeObj[j][k].Yaw, Data.Blocks[i].RailFreeObj[j][k].Pitch,
													Data.Blocks[i].RailFreeObj[j][k].Roll), -1, Data.AccurateObjectDisposal, StartingDistance, EndingDistance,
												Data.BlockInterval, tpos, 1.0, false);
											break;
										case RailTransformationTypes.FollowsBoth:
											Data.Structure.Objects[sttype].CreateObject(wpos, RailTransformation,
												new World.Transformation(Data.Blocks[i].RailFreeObj[j][k].Yaw, Data.Blocks[i].RailFreeObj[j][k].Pitch,
													Data.Blocks[i].RailFreeObj[j][k].Roll), -1, Data.AccurateObjectDisposal, StartingDistance, EndingDistance,
												Data.BlockInterval, tpos, 1.0, false);
											break;
									}
								}
							}
						}
						// sections/signals/transponders
						if (j == 0)
						{
							// signals
							for (int k = 0; k < Data.Blocks[i].Signal.Length; k++)
							{
								SignalData sd;
								if (Data.Blocks[i].Signal[k].SignalCompatibilityObjectIndex >= 0)
								{
									sd = Data.CompatibilitySignalData[Data.Blocks[i].Signal[k].SignalCompatibilityObjectIndex];
								}
								else
								{
									sd = Data.SignalData[Data.Blocks[i].Signal[k].SignalObjectIndex];
								}
								// objects
								double dz = Data.Blocks[i].Signal[k].TrackPosition - StartingDistance;
								if (Data.Blocks[i].Signal[k].ShowPost)
								{
									// post
									double dx = Data.Blocks[i].Signal[k].X;
									Vector3 wpos = pos;
									wpos.X += dx * RailTransformation.X.X + dz * RailTransformation.Z.X;
									wpos.Y += dx * RailTransformation.X.Y + dz * RailTransformation.Z.Y;
									wpos.Z += dx * RailTransformation.X.Z + dz * RailTransformation.Z.Z;
									double tpos = Data.Blocks[i].Signal[k].TrackPosition;
									double b = 0.25 + 0.75 * GetBrightness(ref Data, tpos);
									ObjectManager.CreateStaticObject(SignalPost, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
								}
								if (Data.Blocks[i].Signal[k].ShowObject)
								{
									// signal object
									double dx = Data.Blocks[i].Signal[k].X;
									double dy = Data.Blocks[i].Signal[k].Y;
									Vector3 wpos = pos;
									wpos.X += dx * RailTransformation.X.X + dy * RailTransformation.Y.X + dz * RailTransformation.Z.X;
									wpos.Y += dx * RailTransformation.X.Y + dy * RailTransformation.Y.Y + dz * RailTransformation.Z.Y;
									wpos.Z += dx * RailTransformation.X.Z + dy * RailTransformation.Y.Z + dz * RailTransformation.Z.Z;
									double tpos = Data.Blocks[i].Signal[k].TrackPosition;

									if (sd is CompatibilitySignalData)
									{
										CompatibilitySignalData csd = (CompatibilitySignalData)sd;
										if (csd.Numbers.Length != 0)
										{
											double brightness = 0.25 + 0.75 * GetBrightness(ref Data, tpos);
											ObjectManager.AnimatedObjectCollection aoc = new ObjectManager.AnimatedObjectCollection();
											aoc.Objects = new ObjectManager.AnimatedObject[1];
											aoc.Objects[0] = new ObjectManager.AnimatedObject();
											aoc.Objects[0].States = new ObjectManager.AnimatedObjectState[csd.Numbers.Length];
											for (int l = 0; l < csd.Numbers.Length; l++)
											{
												if (csd.Objects[l] == null)
												{
													continue;
												}
												aoc.Objects[0].States[l].Object = csd.Objects[l].Clone();
											}
											string expr = "";
											for (int l = 0; l < csd.Numbers.Length - 1; l++)
											{
												expr += "section " + csd.Numbers[l].ToString(Culture) + " <= " + l.ToString(Culture) + " ";
											}
											expr += (csd.Numbers.Length - 1).ToString(Culture);
											for (int l = 0; l < csd.Numbers.Length - 1; l++)
											{
												expr += " ?";
											}
											aoc.Objects[0].StateFunction = FunctionScripts.GetFunctionScriptFromPostfixNotation(expr);
											aoc.Objects[0].RefreshRate = 1.0 + 0.01 * Program.RandomNumberGenerator.NextDouble();
											aoc.CreateObject(wpos, RailTransformation, new World.Transformation(Data.Blocks[i].Signal[k].Yaw, Data.Blocks[i].Signal[k].Pitch, Data.Blocks[i].Signal[k].Roll), Data.Blocks[i].Signal[k].Section, Data.AccurateObjectDisposal, StartingDistance, EndingDistance, Data.BlockInterval, tpos, brightness, false);
										}
									}

								}
							}
							// sections
							for (int k = 0; k < Data.Blocks[i].Section.Length; k++)
							{
								int m = Game.Sections.Length;
								Array.Resize<Game.Section>(ref Game.Sections, m + 1);

								// create section
								Game.Sections[m].TrackPosition = Data.Blocks[i].Section[k].TrackPosition;
								Game.Sections[m].Aspects = new Game.SectionAspect[Data.Blocks[i].Section[k].Aspects.Length];
								for (int l = 0; l < Data.Blocks[i].Section[k].Aspects.Length; l++)
								{
									Game.Sections[m].Aspects[l].Number = Data.Blocks[i].Section[k].Aspects[l];
									if (Data.Blocks[i].Section[k].Aspects[l] >= 0 & Data.Blocks[i].Section[k].Aspects[l] < Data.SignalSpeeds.Length)
									{
										Game.Sections[m].Aspects[l].Speed = Data.SignalSpeeds[Data.Blocks[i].Section[k].Aspects[l]];
									}
									else
									{
										Game.Sections[m].Aspects[l].Speed = double.PositiveInfinity;
									}
								}
								Game.Sections[m].Type = Data.Blocks[i].Section[k].Type;
								Game.Sections[m].CurrentAspect = -1;
								if (m > 0)
								{
									Game.Sections[m].PreviousSection = m - 1;
									Game.Sections[m - 1].NextSection = m;
								}
								else
								{
									Game.Sections[m].PreviousSection = -1;
								}
								Game.Sections[m].NextSection = -1;
								Game.Sections[m].StationIndex = Data.Blocks[i].Section[k].DepartureStationIndex;
								Game.Sections[m].Invisible = Data.Blocks[i].Section[k].Invisible;
								Game.Sections[m].Trains = new TrainManager.Train[] { };
								// create section change event
								double d = Data.Blocks[i].Section[k].TrackPosition - StartingDistance;
								int p = TrackManager.CurrentTrack.Elements[n].Events.Length;
								Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, p + 1);
								TrackManager.CurrentTrack.Elements[n].Events[p] = new TrackManager.SectionChangeEvent(d, m - 1, m);
							}

						}
						// limit
						if (j == 0)
						{
							for (int k = 0; k < Data.Blocks[i].Limit.Length; k++)
							{
								if (Data.Blocks[i].Limit[k].Direction != 0)
								{
									double dx = 2.2 * (double)Data.Blocks[i].Limit[k].Direction;
									double dz = Data.Blocks[i].Limit[k].TrackPosition - StartingDistance;
									Vector3 wpos = pos;
									wpos.X += dx * RailTransformation.X.X + dz * RailTransformation.Z.X;
									wpos.Y += dx * RailTransformation.X.Y + dz * RailTransformation.Z.Y;
									wpos.Z += dx * RailTransformation.X.Z + dz * RailTransformation.Z.Z;
									double tpos = Data.Blocks[i].Limit[k].TrackPosition;
									double b = 0.25 + 0.75 * GetBrightness(ref Data, tpos);
									if (Data.Blocks[i].Limit[k].Speed <= 0.0 | Data.Blocks[i].Limit[k].Speed >= 1000.0)
									{
										ObjectManager.CreateStaticObject(LimitPostInfinite, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
									}
									else
									{
										if (Data.Blocks[i].Limit[k].Cource < 0)
										{
											ObjectManager.CreateStaticObject(LimitPostLeft, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
										}
										else if (Data.Blocks[i].Limit[k].Cource > 0)
										{
											ObjectManager.CreateStaticObject(LimitPostRight, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
										}
										else
										{
											ObjectManager.CreateStaticObject(LimitPostStraight, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
										}
										double lim = Data.Blocks[i].Limit[k].Speed / Data.UnitOfSpeed;
										if (lim < 10.0)
										{
											int d0 = (int)Math.Round(lim);
											int o = ObjectManager.CreateStaticObject(LimitOneDigit, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, true);
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 1)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d0 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[0].DaytimeTexture);
											}
										}
										else if (lim < 100.0)
										{
											int d1 = (int)Math.Round(lim);
											int d0 = d1 % 10;
											d1 /= 10;
											int o = ObjectManager.CreateStaticObject(LimitTwoDigits, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, true);
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 1)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d1 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[0].DaytimeTexture);
											}
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 2)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d0 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[1].DaytimeTexture);
											}
										}
										else
										{
											int d2 = (int)Math.Round(lim);
											int d0 = d2 % 10;
											int d1 = (d2 / 10) % 10;
											d2 /= 100;
											int o = ObjectManager.CreateStaticObject(LimitThreeDigits, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, true);
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 1)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d2 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[0].DaytimeTexture);
											}
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 2)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d1 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[1].DaytimeTexture);
											}
											if (ObjectManager.Objects[o].Mesh.Materials.Length >= 3)
											{
												Textures.RegisterTexture(OpenBveApi.Path.CombineFile(LimitGraphicsPath, "limit_" + d0 + ".png"), out ObjectManager.Objects[o].Mesh.Materials[2].DaytimeTexture);
											}
										}
									}
								}
							}
						}
						/*
						// stop
						if (j == 0)
						{
							for (int k = 0; k < Data.Blocks[i].Stop.Length; k++)
							{
								if (Data.Blocks[i].Stop[k].Direction != 0)
								{
									double dx = 1.8 * (double)Data.Blocks[i].Stop[k].Direction;
									double dz = Data.Blocks[i].Stop[k].TrackPosition - StartingDistance;
									Vector3 wpos = pos;
									wpos.X += dx * RailTransformation.X.X + dz * RailTransformation.Z.X;
									wpos.Y += dx * RailTransformation.X.Y + dz * RailTransformation.Z.Y;
									wpos.Z += dx * RailTransformation.X.Z + dz * RailTransformation.Z.Z;
									double tpos = Data.Blocks[i].Stop[k].TrackPosition;
									double b = 0.25 + 0.75 * GetBrightness(ref Data, tpos);
									ObjectManager.CreateStaticObject(StopPost, wpos, RailTransformation, NullTransformation, Data.AccurateObjectDisposal, 0.0, StartingDistance, EndingDistance, Data.BlockInterval, tpos, b, false);
								}
							}
						}
						 */
					}
				}
				// finalize block
				Position.X += Direction.X * c;
				Position.Y += h;
				Position.Z += Direction.Y * c;
				if (a != 0.0)
				{
					World.Rotate(ref Direction, Math.Cos(-a), Math.Sin(-a));
				}
			}
			/*
			// orphaned transponders
			if (!PreviewOnly)
			{
				for (int i = Data.FirstUsedBlock; i < Data.Blocks.Length; i++)
				{
					for (int j = 0; j < Data.Blocks[i].Transponder.Length; j++)
					{
						if (Data.Blocks[i].Transponder[j].Type != -1)
						{
							int n = i - Data.FirstUsedBlock;
							int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
							Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
							double d = Data.Blocks[i].Transponder[j].TrackPosition - TrackManager.CurrentTrack.Elements[n].StartingTrackPosition;
							int s = Data.Blocks[i].Transponder[j].Section;
							if (s >= 0) s = -1;
							TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.TransponderEvent(d, Data.Blocks[i].Transponder[j].Type, Data.Blocks[i].Transponder[j].Data, s, Data.Blocks[i].Transponder[j].ClipToFirstRedSection);
							Data.Blocks[i].Transponder[j].Type = -1;
						}
					}
				}
			}
			 */
			// insert station end events
			for (int i = 0; i < Game.Stations.Length; i++)
			{
				int j = Game.Stations[i].Stops.Length - 1;
				if (j >= 0)
				{
					double p = Game.Stations[i].Stops[j].TrackPosition + Game.Stations[i].Stops[j].ForwardTolerance + Data.BlockInterval;
					int k = (int)Math.Floor(p / (double)Data.BlockInterval) - Data.FirstUsedBlock;
					if (k >= 0 & k < Data.Blocks.Length)
					{
						double d = p - (double)(k + Data.FirstUsedBlock) * (double)Data.BlockInterval;
						int m = TrackManager.CurrentTrack.Elements[k].Events.Length;
						Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[k].Events, m + 1);
						TrackManager.CurrentTrack.Elements[k].Events[m] = new TrackManager.StationEndEvent(d, i);
					}
				}
			}
			// create default point of interests
			if (Game.PointsOfInterest.Length == 0)
			{
				Game.PointsOfInterest = new OpenBve.Game.PointOfInterest[Game.Stations.Length];
				int n = 0;
				for (int i = 0; i < Game.Stations.Length; i++)
				{
					if (Game.Stations[i].Stops.Length != 0)
					{
						Game.PointsOfInterest[n].Text = Game.Stations[i].Name;
						Game.PointsOfInterest[n].TrackPosition = Game.Stations[i].Stops[0].TrackPosition;
						Game.PointsOfInterest[n].TrackOffset = new Vector3(0.0, 2.8, 0.0);
						if (Game.Stations[i].OpenLeftDoors & !Game.Stations[i].OpenRightDoors)
						{
							Game.PointsOfInterest[n].TrackOffset.X = -2.5;
						}
						else if (!Game.Stations[i].OpenLeftDoors & Game.Stations[i].OpenRightDoors)
						{
							Game.PointsOfInterest[n].TrackOffset.X = 2.5;
						}
						n++;
					}
				}
				Array.Resize<Game.PointOfInterest>(ref Game.PointsOfInterest, n);
			}
			// convert block-based cant into point-based cant
			for (int i = CurrentTrackLength - 1; i >= 1; i--)
			{
				if (TrackManager.CurrentTrack.Elements[i].CurveCant == 0.0)
				{
					TrackManager.CurrentTrack.Elements[i].CurveCant = TrackManager.CurrentTrack.Elements[i - 1].CurveCant;
				}
				else if (TrackManager.CurrentTrack.Elements[i - 1].CurveCant != 0.0)
				{
					if (Math.Sign(TrackManager.CurrentTrack.Elements[i - 1].CurveCant) == Math.Sign(TrackManager.CurrentTrack.Elements[i].CurveCant))
					{
						if (Math.Abs(TrackManager.CurrentTrack.Elements[i - 1].CurveCant) > Math.Abs(TrackManager.CurrentTrack.Elements[i].CurveCant))
						{
							TrackManager.CurrentTrack.Elements[i].CurveCant = TrackManager.CurrentTrack.Elements[i - 1].CurveCant;
						}
					}
					else
					{
						TrackManager.CurrentTrack.Elements[i].CurveCant = 0.5 * (TrackManager.CurrentTrack.Elements[i].CurveCant + TrackManager.CurrentTrack.Elements[i - 1].CurveCant);
					}
				}
			}
			// finalize
			Array.Resize<TrackManager.TrackElement>(ref TrackManager.CurrentTrack.Elements, CurrentTrackLength);
			for (int i = 0; i < Game.Stations.Length; i++)
			{
				if (Game.Stations[i].Stops.Length == 0 & Game.Stations[i].StopMode != Game.StationStopMode.AllPass)
				{
					Interface.AddMessage(Interface.MessageType.Warning, false, "Station " + Game.Stations[i].Name + " expects trains to stop but does not define stop points at track position " + Game.Stations[i].DefaultTrackPosition.ToString(Culture) + " in file " + FileName);
					Game.Stations[i].StopMode = Game.StationStopMode.AllPass;
				}
				if (Game.Stations[i].StationType == Game.StationType.ChangeEnds)
				{
					if (i < Game.Stations.Length - 1)
					{
						if (Game.Stations[i + 1].StopMode != Game.StationStopMode.AllStop)
						{
							Interface.AddMessage(Interface.MessageType.Warning, false, "Station " + Game.Stations[i].Name + " is marked as \"change ends\" but the subsequent station does not expect all trains to stop in file " + FileName);
							Game.Stations[i + 1].StopMode = Game.StationStopMode.AllStop;
						}
					}
					else
					{
						Interface.AddMessage(Interface.MessageType.Warning, false, "Station " + Game.Stations[i].Name + " is marked as \"change ends\" but there is no subsequent station defined in file " + FileName);
						Game.Stations[i].StationType = Game.StationType.Terminal;
					}
				}
			}
			if (Game.Stations.Length != 0)
			{
				Game.Stations[Game.Stations.Length - 1].StationType = Game.StationType.Terminal;
			}
			if (TrackManager.CurrentTrack.Elements.Length != 0)
			{
				int n = TrackManager.CurrentTrack.Elements.Length - 1;
				int m = TrackManager.CurrentTrack.Elements[n].Events.Length;
				Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[n].Events, m + 1);
				TrackManager.CurrentTrack.Elements[n].Events[m] = new TrackManager.TrackEndEvent(Data.BlockInterval);
			}
			// insert compatibility beacons
			if (!PreviewOnly)
			{
				List<TrackManager.TransponderEvent> transponders = new List<TrackManager.TransponderEvent>();
				bool atc = false;
				for (int i = 0; i < TrackManager.CurrentTrack.Elements.Length; i++)
				{
					for (int j = 0; j < TrackManager.CurrentTrack.Elements[i].Events.Length; j++)
					{
						if (!atc)
						{
							if (TrackManager.CurrentTrack.Elements[i].Events[j] is TrackManager.StationStartEvent)
							{
								TrackManager.StationStartEvent station = (TrackManager.StationStartEvent)TrackManager.CurrentTrack.Elements[i].Events[j];
								if (Game.Stations[station.StationIndex].SafetySystem == Game.SafetySystem.Atc)
								{
									Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[i].Events, TrackManager.CurrentTrack.Elements[i].Events.Length + 2);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 2] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 0, 0, false);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 1] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 1, 0, false);
									atc = true;
								}
							}
						}
						else
						{
							if (TrackManager.CurrentTrack.Elements[i].Events[j] is TrackManager.StationStartEvent)
							{
								TrackManager.StationStartEvent station = (TrackManager.StationStartEvent)TrackManager.CurrentTrack.Elements[i].Events[j];
								if (Game.Stations[station.StationIndex].SafetySystem == Game.SafetySystem.Ats)
								{
									Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[i].Events, TrackManager.CurrentTrack.Elements[i].Events.Length + 2);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 2] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 2, 0, false);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 1] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 3, 0, false);
								}
							}
							else if (TrackManager.CurrentTrack.Elements[i].Events[j] is TrackManager.StationEndEvent)
							{
								TrackManager.StationEndEvent station = (TrackManager.StationEndEvent)TrackManager.CurrentTrack.Elements[i].Events[j];
								if (Game.Stations[station.StationIndex].SafetySystem == Game.SafetySystem.Atc)
								{
									Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[i].Events, TrackManager.CurrentTrack.Elements[i].Events.Length + 2);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 2] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 1, 0, false);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 1] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 2, 0, false);
								}
								else if (Game.Stations[station.StationIndex].SafetySystem == Game.SafetySystem.Ats)
								{
									Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[i].Events, TrackManager.CurrentTrack.Elements[i].Events.Length + 2);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 2] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 3, 0, false);
									TrackManager.CurrentTrack.Elements[i].Events[TrackManager.CurrentTrack.Elements[i].Events.Length - 1] = new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcTrackStatus, 0, 0, false);
									atc = false;
								}
							}
							else if (TrackManager.CurrentTrack.Elements[i].Events[j] is TrackManager.LimitChangeEvent)
							{
								TrackManager.LimitChangeEvent limit = (TrackManager.LimitChangeEvent)TrackManager.CurrentTrack.Elements[i].Events[j];
								int speed = (int)Math.Round(Math.Min(4095.0, 3.6 * limit.NextSpeedLimit));
								int distance = Math.Min(1048575, (int)Math.Round(TrackManager.CurrentTrack.Elements[i].StartingTrackPosition + limit.TrackPositionDelta));
								unchecked
								{
									int value = (int)((uint)speed | ((uint)distance << 12));
									transponders.Add(new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtcSpeedLimit, value, 0, false));
								}
							}
						}
						if (TrackManager.CurrentTrack.Elements[i].Events[j] is TrackManager.TransponderEvent)
						{
							TrackManager.TransponderEvent transponder = TrackManager.CurrentTrack.Elements[i].Events[j] as TrackManager.TransponderEvent;
							if (transponder.Type == TrackManager.SpecialTransponderTypes.InternalAtsPTemporarySpeedLimit)
							{
								int speed = Math.Min(4095, transponder.Data);
								int distance = Math.Min(1048575, (int)Math.Round(TrackManager.CurrentTrack.Elements[i].StartingTrackPosition + transponder.TrackPositionDelta));
								unchecked
								{
									int value = (int)((uint)speed | ((uint)distance << 12));
									transponders.Add(new TrackManager.TransponderEvent(0.0, TrackManager.SpecialTransponderTypes.AtsPTemporarySpeedLimit, value, 0, false));
								}
							}
						}
					}
				}
				int n = TrackManager.CurrentTrack.Elements[0].Events.Length;
				Array.Resize<TrackManager.GeneralEvent>(ref TrackManager.CurrentTrack.Elements[0].Events, n + transponders.Count);
				for (int i = 0; i < transponders.Count; i++)
				{
					TrackManager.CurrentTrack.Elements[0].Events[n + i] = transponders[i];
				}
			}
			// cant
			if (!PreviewOnly)
			{
				ComputeCantTangents();
				int subdivisions = (int)Math.Floor(Data.BlockInterval / 5.0);
				if (subdivisions >= 2)
				{
					SmoothenOutTurns(subdivisions);
					ComputeCantTangents();
				}
			}
		}
	}
}