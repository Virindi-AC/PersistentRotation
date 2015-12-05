﻿using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UnityEngine;
using System.IO;

namespace PersistentRotation
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class Data : MonoBehaviour
    {
        public static Data instance { get; private set; }

        String path
        {
            get { return KSPUtil.ApplicationRootPath + "/GameData/PersistentRotation/PersistentRotation_" + HighLogic.CurrentGame.Title.TrimEnd("_()SANDBOXCAREERSCIENCE".ToCharArray()).TrimEnd(' ') + ".cfg"; }
        }
        String backup_path
        {
            get { return KSPUtil.ApplicationRootPath + "/GameData/PersistentRotation/Backup_PersistentRotation_" + HighLogic.CurrentGame.Title.TrimEnd("_()SANDBOXCAREERSCIENCE".ToCharArray()).TrimEnd(' ') + ".cfg"; }
        }

        public Dictionary<string, Vector3> momentum = new Dictionary<string, Vector3>();
        public Dictionary<string, Quaternion> rotation = new Dictionary<string, Quaternion>();
        public Dictionary<string, Vector3> direction = new Dictionary<string, Vector3>();
        public Dictionary<string, ITargetable> reference = new Dictionary<string, ITargetable>();

        private void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            Load();
            Clean();

            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
            {
                momentum[FlightGlobals.ActiveVessel.id.ToString()] = Vector3.zero;
            }
        }

        private void OnDestroy()
        {
            instance = null;
            Save();

            File.Delete(backup_path);
            File.Copy(path, backup_path);
        }

        public void Save()
        {
            Debug.Log("[PR] Saving Data.");
            try
            {
                ConfigNode save = new ConfigNode();
                save.AddValue("TIME", Planetarium.GetUniversalTime());
                ConfigNode.CreateConfigFromObject(this, 0, save);

                //Save momentum of all vessels
                ConfigNode cn_momentum = save.AddNode("MOMENTUM");
                foreach (KeyValuePair<string, Vector3> entry in momentum)
                {
                    cn_momentum.AddValue(entry.Key, KSPUtil.WriteVector(entry.Value));
                }
                //Save rotation of all vessels
                ConfigNode cn_rotation = save.AddNode("ROTATION");
                foreach (KeyValuePair<string, Quaternion> entry in rotation)
                {
                    cn_rotation.AddValue(entry.Key, KSPUtil.WriteQuaternion(entry.Value));
                }
                //Save direction to the mainBody for each vessel
                ConfigNode cn_direction = save.AddNode("DIRECTION");
                foreach (KeyValuePair<string, Vector3> entry in direction)
                {
                    cn_direction.AddValue(entry.Key, KSPUtil.WriteVector(entry.Value));
                }
                //Save direction reference object for each vessel
                ConfigNode cn_reference = save.AddNode("REFERENCE");
                foreach (KeyValuePair<string, ITargetable> entry in reference)
                {
                    if (entry.Value != null)
                    {
                        if (entry.Value.GetType() == typeof(CelestialBody))
                        {
                            cn_reference.AddValue(entry.Key, entry.Value.GetName());
                        }
                        else if (entry.Value.GetType() == typeof(Vessel))
                        {
                            cn_reference.AddValue(entry.Key, entry.Value.GetVessel().id.ToString());
                        }
                        else
                        {
                            Debug.LogError("[PR] Wrong Reference Type!");
                            cn_reference.AddValue(entry.Key, "NONE");
                        }
                    }
                    else
                    {
                        cn_reference.AddValue(entry.Key, "NONE");
                    }
                }
                save.Save(path);
            }
            catch (Exception e) { Debug.Log("[PR] Saving not sucessfull: " + e.Message); }
        }
        public void Load()
        {
            //This is called when all persistent rotation data is being loaded from the cfg file.

            Debug.Log("[PR]: Loading Data.");

            momentum.Clear();
            rotation.Clear();
            direction.Clear();
            reference.Clear();

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                Generate(vessel);
            }

            //Open cfg file
            ConfigNode load = ConfigNode.Load(path);
            if (load == null)
            {
                Debug.Log("[PR] Couldn't load data: File not found.");
                return;
            }
            if(float.Parse(load.GetValue("TIME")) >= Planetarium.GetUniversalTime())
            {
                Debug.Log("[PR] Loading Backup!");
                load = null;

                load = ConfigNode.Load(backup_path);
                if (load == null)
                {
                    Debug.Log("[PR] Couldn't load data: File not found.");
                    return;
                }
            }
            //Load momentum
            ConfigNode cn_momentum = load.GetNode("MOMENTUM");
            foreach (ConfigNode.Value s in cn_momentum.values)
            {
                momentum[s.name] = KSPUtil.ParseVector3(s.value);
            }
            //Load rotation
            ConfigNode cn_rotation = load.GetNode("ROTATION");
            foreach (ConfigNode.Value s in cn_rotation.values)
            {
                rotation[s.name] = KSPUtil.ParseQuaternion(s.value);
            }
            //Load direction
            ConfigNode cn_direction = load.GetNode("DIRECTION");
            foreach (ConfigNode.Value s in cn_direction.values)
            {
                direction[s.name] = KSPUtil.ParseVector3(s.value);
            }
            //Load reference
            ConfigNode cn_reference = load.GetNode("REFERENCE");
            foreach (ConfigNode.Value s in cn_reference.values)
            {
                if (s.value != "NONE")
                {
                    reference[s.name] = null;
                    foreach (CelestialBody body in FlightGlobals.Bodies) //Check all bodies
                    {
                        if (body.name == s.value)
                        {
                            reference[s.name] = body;
                        }
                    }

                    foreach (Vessel vessel in FlightGlobals.Vessels) // Check all vessels
                    {
                        if (vessel.id.ToString() == s.value)
                        {
                            reference[s.name] = vessel;
                        }
                    }
                }
                else
                {
                    reference[s.name] = null;
                }
            }
            //Debug.Log("Load out");
        }
        public void Clean()
        {

            Debug.Log("[PR] Cleaning Data.");

            //Clean vessel momentum
            Dictionary<string, Vector3> temp_momentum = new Dictionary<string, Vector3>();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (vessel.vesselType == VesselType.SpaceObject || vessel.vesselType == VesselType.Unknown)
                {
                    float ran_x = (float)UnityEngine.Random.Range(0, 10) / (float)100;
                    float ran_y = (float)UnityEngine.Random.Range(0, 10) / (float)100;
                    float ran_z = (float)UnityEngine.Random.Range(0, 10) / (float)100;

                    temp_momentum[vessel.id.ToString()] = new Vector3(ran_x, ran_y, ran_z);
                }
                else
                {
                    temp_momentum[vessel.id.ToString()] = Vector3.zero;
                }
            }
            foreach (KeyValuePair<string, Vector3> entry in momentum)
            {
                temp_momentum[entry.Key] = entry.Value;
            }
            momentum.Clear();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                momentum[vessel.id.ToString()] = temp_momentum[vessel.id.ToString()];
            }
            temp_momentum.Clear();
            //Clean vessel rotation
            Dictionary<string, Quaternion> temp_rotation = new Dictionary<string, Quaternion>();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                temp_rotation[vessel.id.ToString()] = vessel.transform.rotation;
            }
            foreach (KeyValuePair<string, Quaternion> entry in rotation)
            {
                temp_rotation[entry.Key] = entry.Value;
            }
            rotation.Clear();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                rotation[vessel.id.ToString()] = temp_rotation[vessel.id.ToString()];
            }
            temp_rotation.Clear();
            //Clean vessel direction
            Dictionary<string, Vector3> temp_direction = new Dictionary<string, Vector3>();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                temp_direction[vessel.id.ToString()] = (vessel.mainBody.position - vessel.transform.position).normalized;
            }
            foreach (KeyValuePair<string, Vector3> entry in direction)
            {
                temp_direction[entry.Key] = entry.Value;
            }
            direction.Clear();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                direction[vessel.id.ToString()] = temp_direction[vessel.id.ToString()];
            }
            temp_direction.Clear();
            //Clean vessel reference
            Dictionary<string, ITargetable> temp_reference = new Dictionary<string, ITargetable>();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                temp_reference[vessel.id.ToString()] = null;
            }
            foreach (KeyValuePair<string, ITargetable> entry in reference)
            {
                temp_reference[entry.Key] = entry.Value;
            }
            reference.Clear();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                reference[vessel.id.ToString()] = temp_reference[vessel.id.ToString()];
            }
            temp_reference.Clear();
        }
        public void Generate(Vessel vessel)
        {
            if(rotation.ContainsKey(vessel.id.ToString()) && momentum.ContainsKey(vessel.id.ToString()) && reference.ContainsKey(vessel.id.ToString()) && direction.ContainsKey(vessel.id.ToString()))
            {
                Debug.Log("[PR] " + vessel.vesselName + " already has data.");
                return;
            }

            Debug.Log("[PR] Generating data for " + vessel.vesselName);

            rotation[vessel.id.ToString()] = vessel.transform.rotation;
            direction[vessel.id.ToString()] = (vessel.mainBody.position - vessel.transform.position).normalized;
            reference[vessel.id.ToString()] = null;

            if (vessel.vesselType == VesselType.SpaceObject || vessel.vesselType == VesselType.Unknown)
            {
                float ran_x = (float)UnityEngine.Random.Range(0, 10) / (float)100;
                float ran_y = (float)UnityEngine.Random.Range(0, 10) / (float)100;
                float ran_z = (float)UnityEngine.Random.Range(0, 10) / (float)100;

                momentum[vessel.id.ToString()] = new Vector3(ran_x, ran_y, ran_z);
            }
            else
            {
                momentum[vessel.id.ToString()] = Vector3.zero;
            }
        }
    }
}
