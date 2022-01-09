using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DS3RagdollConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("Please drag and drop a 2010 Havok ragdoll XML file onto the exe.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            else if (VerifyInput(args[0]))
            {
                XElement hkaDataSection2014 = ConvertHka();
                XElement hknpDataSection = GetHknpPhysics();

                BuildObjDict(hknpDataSection, "hknp");
                BuildObjDict(hkaDataSection2014, "hka");


                RenameObjects(hkaDataSection2014.Element("hkobject"), "hka");
                XElement hknpPhysicsSceneData = hknpDataSection.Elements().First(hkobj => hkobj.Attribute("class").Value == "hknpPhysicsSceneData");
                RenameObjects(hknpPhysicsSceneData, "hknp");
                IEnumerable<XElement> hkaSkeletonMapperVariants = hkaDataSection2014.Element("hkobject").Element("hkparam").Elements().ToList()[2].ElementsAfterSelf();
                foreach (XElement xelem in hkaSkeletonMapperVariants)
                {
                    RenameObjects(xelem, "hka");
                }

                BuildOutputPackfile(hknpDataSection, hkaDataSection2014);

                File.Delete(args[0] + ".bak");
                File.Move(args[0], args[0] + ".bak");

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Encoding = new ASCIIEncoding();
                settings.Indent = true;
                using (var writer = XmlWriter.Create(args[0], settings))
                {
                    OutputRoot.Save(writer);
                }
            }
            
        }

        public static XElement InputRoot { get; set; }

        public static XElement OutputRoot { get; set; }

        public static Dictionary<string, XElement> HkaObjDict { get; set; } = new();

        public static Dictionary<string, XElement> HknpObjDict { get; set; } = new();

        public static Dictionary<string, XElement> RenamedObjDict { get; set; } = new();

        public static List<string> RagdollBoneList { get; set; } = new();

        private static int CurrentName { get; set; } = 90;

        static bool VerifyInput(string inputfile)
        {
            try
            {
                InputRoot = XElement.Load(inputfile);

                // checks whether the XML is a 2010 havok packfile 
                if (InputRoot.Name.ToString() == "hkpackfile" && InputRoot.Attribute("contentsversion").Value.Contains("hk_2010"))
                {
                    return true;
                }
                else
                {
                    Console.WriteLine($"{inputfile} is not a valid 2010 Havok ragdoll XML file.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return false;
                }
            }
            // if no arguments were given
            catch (ArgumentNullException)
            {
                Console.WriteLine("Please drag and drop a 2010 Havok ragdoll XML file onto the exe.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
            }
            // if input file is an invalid format
            catch (UriFormatException)
            {
                Console.WriteLine($"{inputfile} is not an XML file.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
            }
        }

        // stores each object under its original name Id 
        static void BuildObjDict(XElement __data__, string dictType)
        {
            foreach (XElement hkojbect in __data__.Elements())
            {
                if (dictType == "hka")
                {
                    HkaObjDict.Add(hkojbect.Attribute("name").Value, hkojbect);
                }
                else if (dictType == "hknp")
                {
                    HknpObjDict.Add(hkojbect.Attribute("name").Value, hkojbect);
                }
            }
        }

        // creates a Havok 2014 ragdoll file from hknp physics data and converted hka data
        static void BuildOutputPackfile(XElement hknpData, XElement hkaData)
        {
            OutputRoot = new XElement("hkpackfile",
                         new XAttribute("classversion", "11"),
                         new XAttribute("contentsversion", "hk_2014.1.0-r1"));
            hkaData.Add(hknpData.Elements());
            XElement[] ObjArray = hkaData.Elements().OrderBy(e => int.Parse(e.Attribute("name").Value[1..])).ToArray();
            hkaData.ReplaceAll(ObjArray);
            hkaData.Add(new XAttribute("name", "__data__"));
            OutputRoot.Add(hkaData);
        }

        // separates physics data and converts it to hknp format using hkp2hknp
        static XElement GetHknpPhysics()
        {
            XElement hkpackfileHkpPhysics = new XElement(InputRoot);
            XElement hkRootLevelElement = hkpackfileHkpPhysics.Elements()
                                          .FirstOrDefault(hksection => hksection.Attribute("name").Value == "__data__")
                                          .Elements()
                                          .FirstOrDefault(hkobj => hkobj.Attribute("name").Value == InputRoot.Attribute("toplevelobject").Value);

            // removes references to all hka elements from hkRootLevelContainer
            hkRootLevelElement.Element("hkparam").Attribute("numelements").Value = "1";
            List <XElement> namedVariants = hkRootLevelElement.Element("hkparam").Elements().ToList();
            for (int i = 0;  i < namedVariants.Count; i++)
            {
                string className = namedVariants[i].Elements()
                                   .FirstOrDefault(hkparam => hkparam.Attribute("name").Value == "className")
                                   .Value;

                if (className != "hkpPhysicsData")
                {
                    namedVariants[i].Remove();
                }
            }


            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            if (!Directory.Exists(baseDirectory + "temp"))
            {
                Directory.CreateDirectory(baseDirectory + "temp");
            }

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new ASCIIEncoding();
            settings.Indent = true;
            using (var writer = XmlWriter.Create(baseDirectory + "temp\\hkpPhysics.xml", settings))
            {
                hkpackfileHkpPhysics.Save(writer);
            }

            Process hkp2hknp = new Process();
            hkp2hknp.StartInfo.FileName = baseDirectory + "Dependencies\\hkp2hknp.exe";
            string args = "\"" + baseDirectory + "temp\\hkpPhysics.xml\" " + "\"" + baseDirectory + "temp\\hknpPhysics.xml\"";
            // Console.WriteLine(baseDirectory + "Dependencies/hkp2hknp.exe " + args);
            hkp2hknp.StartInfo.Arguments = args;
            hkp2hknp.StartInfo.WorkingDirectory = baseDirectory + "Dependencies";
            hkp2hknp.StartInfo.RedirectStandardOutput = true;
            hkp2hknp.Start();
            hkp2hknp.WaitForExit();

            XElement hkpackfileHknpPhysics = XElement.Load(baseDirectory + "temp\\hknpPhysics.xml");

            Directory.Delete(baseDirectory + "temp", true);

            XElement hknpPhysicsData = hkpackfileHknpPhysics.Element("hksection");

            hknpPhysicsData.Elements().First(hkobj => hkobj.Attribute("class").Value == "hkRootLevelContainer").Remove();

            hknpPhysicsData.Elements().First(hkobj => hkobj.Attribute("class").Value == "hknpPhysicsSceneData").Attribute("signature").Value = "0x701ce72c";

            ConvertRagdollData(hknpPhysicsData);

            foreach (XElement hknpCapsuleShape in hknpPhysicsData.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hknpCapsuleShape"))
            {
                hknpCapsuleShape.Attribute("signature").Value = "0x60a75f4c";
                hknpCapsuleShape.Elements().First(hkparam => hkparam.Attribute("name").Value == "flags").Value = "451";
                hknpCapsuleShape.Elements().First(hkparam => hkparam.Attribute("name").Value == "dispatchType").Value = "1";
            }

            foreach (XElement hknpConvexPolytopeShape in hknpPhysicsData.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hknpConvexPolytopeShape"))
            {
                hknpConvexPolytopeShape.Attribute("signature").Value = "0x3ce9b3e3";
                hknpConvexPolytopeShape.Elements().First(hkparam => hkparam.Attribute("name").Value == "flags").Value = "451";
                hknpConvexPolytopeShape.Elements().First(hkparam => hkparam.Attribute("name").Value == "dispatchType").Value = "1";
            }

            foreach (XElement hknpShapeMassProperties in hknpPhysicsData.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hknpShapeMassProperties"))
            {
                hknpShapeMassProperties.Attribute("signature").Value = "0xe9191728";
                XElement hkCompressedMassProperties = hknpShapeMassProperties.Element("hkparam").Element("hkobject");
                hkCompressedMassProperties.Add(new XAttribute("class", "hkCompressedMassProperties"), new XAttribute("name", "compressedMassProperties"), new XAttribute("signature", "0x9ac5cee1"));

                List<XElement> multiParams = new();
                foreach (XElement hkparam in hkCompressedMassProperties.Elements().Where(hkparam => hkparam.Value.Split().Length > 1))
                {
                    multiParams.Add(hkparam);
                    string[] paramArray = hkparam.Value.Split();
                    for (int i = 0; i < paramArray.Length; i++)
                    {
                        string value = paramArray[i];
                        string name = hkparam.Attribute("name").Value + (i + 1).ToString();
                        hkCompressedMassProperties.Add(new XElement("hkparam", new XAttribute("name", name), value));
                    }
                }
                foreach (XElement hkparam in multiParams)
                {
                    hkparam.Remove();
                }
            }

            foreach (XElement hkpLimitedHingeConstraintData in hknpPhysicsData.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hkpLimitedHingeConstraintData"))
            {
                hkpLimitedHingeConstraintData.Attribute("signature").Value = "0x51ea603a";

                XElement atoms = hkpLimitedHingeConstraintData.Elements().First(hkparam => hkparam.Attribute("name").Value == "atoms").Element("hkobject");
                atoms.Add(new XAttribute("class", "hkpLimitedHingeConstraintDataAtoms"), new XAttribute("name", "atoms"), new XAttribute("signature", "0x28231532"));

                XElement transforms = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "transforms").Element("hkobject");
                transforms.Add(new XAttribute("class", "hkpSetLocalTransformsConstraintAtom"), new XAttribute("name", "transforms"), new XAttribute("signature", "0x13cd1821"));

                XElement setupStabilization = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "setupStabilization").Element("hkobject");
                setupStabilization.Add(new XAttribute("class", "hkpSetupStabilizationAtom"), new XAttribute("name", "setupStabilization"), new XAttribute("signature", "0x870ee10a"));
                setupStabilization.Elements().First(hkparam => hkparam.Attribute("name").Value == "enabled").Value = "true";

                XElement angMotor = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "angMotor").Element("hkobject");
                angMotor.Add(new XAttribute("class", "hkpAngMotorConstraintAtom"), new XAttribute("name", "angMotor"), new XAttribute("signature", "0x42498456"));

                XElement angFriction = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "angFriction").Element("hkobject");
                angFriction.Add(new XAttribute("class", "hkpAngFrictionConstraintAtom"), new XAttribute("name", "angFriction"), new XAttribute("signature", "0x89f70523"));

                XElement angLimit = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "angLimit").Element("hkobject");
                angLimit.Add(new XAttribute("class", "hkpAngLimitConstraintAtom"), new XAttribute("name", "angLimit"), new XAttribute("signature", "0x1c5a0dd"));

                XElement Ang2d = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "2dAng").Element("hkobject");
                Ang2d.Add(new XAttribute("class", "hkp2dAngConstraintAtom"), new XAttribute("name", "2dAng"), new XAttribute("signature", "0xd277c114"));

                XElement ballSocket = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "ballSocket").Element("hkobject");
                ballSocket.Add(new XAttribute("class", "hkpBallSocketConstraintAtom"), new XAttribute("name", "ballSocket"), new XAttribute("signature", "0x6ba88f7a"));
                ballSocket.Elements().First(hkparam => hkparam.Attribute("name").Value == "solvingMethod").Value = "0";
                XElement velocityStabilizationFactor = ballSocket.Elements().First(hkparam => hkparam.Attribute("name").Value == "velocityStabilizationFactor").Element("hkobject");
                velocityStabilizationFactor.Add(new XAttribute("class", "hkUFloat8"), new XAttribute("name", "velocityStabilizationFactor"), new XAttribute("signature", "0x7c076f9a"));
            }

            foreach (XElement hkpRagdollConstraintData in hknpPhysicsData.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hkpRagdollConstraintData"))
            {
                hkpRagdollConstraintData.Attribute("signature").Value = "0xb77d2036";

                XElement atoms = hkpRagdollConstraintData.Elements().First(hkparam => hkparam.Attribute("name").Value == "atoms").Element("hkobject");
                atoms.Add(new XAttribute("class", "hkpRagdollConstraintDataAtoms"), new XAttribute("name", "atoms"), new XAttribute("signature", "0xe11fb3ac"));

                XElement transforms = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "transforms").Element("hkobject");
                transforms.Add(new XAttribute("class", "hkpSetLocalTransformsConstraintAtom"), new XAttribute("name", "transforms"), new XAttribute("signature", "0x13cd1821"));

                XElement setupStabilization = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "setupStabilization").Element("hkobject");
                setupStabilization.Add(new XAttribute("class", "hkpSetupStabilizationAtom"), new XAttribute("name", "setupStabilization"), new XAttribute("signature", "0x870ee10a"));
                setupStabilization.Elements().First(hkparam => hkparam.Attribute("name").Value == "enabled").Value = "true";

                XElement ragdollMotors = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "ragdollMotors").Element("hkobject");
                ragdollMotors.Add(new XAttribute("class", "hkpRagdollMotorConstraintAtom"), new XAttribute("name", "ragdollMotors"), new XAttribute("signature", "0x9d94d42c"));
                XElement motors = ragdollMotors.Elements().First(hkparam => hkparam.Attribute("name").Value == "motors");
                string[] motorRefArray = motors.Value.Split();
                for (int i = 0; i < motorRefArray.Length; i++)
                {
                    ragdollMotors.Add(new XElement("hkparam", new XAttribute("name", "motors" + (i + 1).ToString())));
                }
                motors.Remove();

                XElement angFriction = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "angFriction").Element("hkobject");
                angFriction.Add(new XAttribute("class", "hkpAngFrictionConstraintAtom"), new XAttribute("name", "angFriction"), new XAttribute("signature", "0x89f70523"));

                XElement twistLimit = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "twistLimit").Element("hkobject");
                twistLimit.Add(new XAttribute("class", "hkpTwistLimitConstraintAtom"), new XAttribute("name", "twistLimit"), new XAttribute("signature", "0xda910271"));

                XElement coneLimit = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "coneLimit").Element("hkobject");
                coneLimit.Add(new XAttribute("class", "hkpConeLimitConstraintAtom"), new XAttribute("name", "coneLimit"), new XAttribute("signature", "0x159ea5c9"));

                XElement planesLimit = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "planesLimit").Element("hkobject");
                if (planesLimit.Elements().First(hkparam => hkparam.Attribute("name").Value == "type").Value == "TYPE_CONE_LIMIT")
                {
                    planesLimit.Add(new XAttribute("class", "hkpConeLimitConstraintAtom"), new XAttribute("name", "planesLimit"), new XAttribute("signature", "0x159ea5c9"));
                }
                else
                {
                    Console.WriteLine($"plane limit at {hkpRagdollConstraintData.Attribute("name").Value} is not of type coneLimit");
                    Console.ReadLine();
                }

                XElement ballSocket = atoms.Elements().First(hkparam => hkparam.Attribute("name").Value == "ballSocket").Element("hkobject");
                ballSocket.Add(new XAttribute("class", "hkpBallSocketConstraintAtom"), new XAttribute("name", "ballSocket"), new XAttribute("signature", "0x6ba88f7a"));
                ballSocket.Elements().First(hkparam => hkparam.Attribute("name").Value == "solvingMethod").Value = "0";
                XElement velocityStabilizationFactor = ballSocket.Elements().First(hkparam => hkparam.Attribute("name").Value == "velocityStabilizationFactor").Element("hkobject");
                velocityStabilizationFactor.Add(new XAttribute("class", "hkUFloat8"), new XAttribute("name", "velocityStabilizationFactor"), new XAttribute("signature", "0x7c076f9a"));
            }

            return hknpPhysicsData;
        }

        // converts 2010 hka data to 2014 hka format, returns an hkRootLevelContainer
        static XElement ConvertHka()
        {
            XElement inputDataSection = InputRoot.Elements("hksection").FirstOrDefault(hksection => hksection.Attribute("name").Value == "__data__");
            XElement outputDataSection = new XElement("hksection", new XAttribute("name", "__data__"));

            // creates a copy of the hkRootLevelContainer from the input file, adjusts the variants to match the variants found in the 2014 version and adds it to the output __data__ hksection
            XElement inputHkRootLevelContainer = inputDataSection.Elements().FirstOrDefault(hkobj => hkobj.Attribute("name").Value == InputRoot.Attribute("toplevelobject").Value);
            XElement outputHkRootLevelContainer = new XElement(inputHkRootLevelContainer);
            XElement ragdollIns = outputHkRootLevelContainer.Element("hkparam").Elements()
                                  .FirstOrDefault(hkobj => hkobj.Value.Contains("hkaRagdollInstance"));
            ragdollIns.Elements().FirstOrDefault(hkparam => hkparam.Value == "hkaRagdollInstance").Value = "hknpRagdollData";
            ragdollIns.Elements().FirstOrDefault(hkparam => hkparam.Value == "RagdollInstance").Value = "Physics Ragdoll";

            XElement physicsData = outputHkRootLevelContainer.Element("hkparam").Elements()
                                  .FirstOrDefault(hkobj => hkobj.Value.Contains("hkpPhysicsData"));
            physicsData.Elements().FirstOrDefault(hkparam => hkparam.Value == "hkpPhysicsData").Value = "hknpPhysicsSceneData";
            physicsData.Elements().FirstOrDefault(hkparam => hkparam.Value == "Physics Data").Value = "Physics Scene Data";
            outputDataSection.Add(outputHkRootLevelContainer);

            // converts the hkaAnimationContainer including its hkaSkeleton children to the 2014 format and adds them to the output __data__ hksection
            XElement hkaAnimationContainer = new XElement(inputDataSection.Elements().FirstOrDefault(hkobj => hkobj.Attribute("class").Value == "hkaAnimationContainer"));
            hkaAnimationContainer.Attribute("signature").Value = "0x26859f4c";
            outputDataSection.Add(hkaAnimationContainer);

            foreach (XElement hkaSkeleton in inputDataSection.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hkaSkeleton"))
            {
                hkaSkeleton.Attribute("signature").Value = "0xfec1cedb";
                hkaSkeleton.Add(new XElement("hkparam", new XAttribute("name", "partitions"), new XAttribute("numelements", "0")));

                // saves list of bone names for boneToBodyMap
                if (hkaSkeleton.Elements().First(hkparam => hkparam.Attribute("name").Value == "name").Value.StartsWith("Ragdoll"))
                {
                    foreach (XElement bone in hkaSkeleton.Elements().First(hkparam => hkparam.Attribute("name").Value == "bones").Elements())
                    {
                        string name = bone.Element("hkparam").Value;
                        RagdollBoneList.Add(name);
                    }
                }

                outputDataSection.Add(hkaSkeleton);
            }

            // converts the hkaSkeletonMappers to the 2014 format
            foreach (XElement hkaSkeletonMapper in inputDataSection.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hkaSkeletonMapper"))
            {
                hkaSkeletonMapper.Attribute("signature").Value = "0xace9849c";
                XElement hkaSkeletonMapperData = hkaSkeletonMapper.Element("hkparam").Element("hkobject");
                hkaSkeletonMapperData.SetAttributeValue("class", "hkaSkeletonMapperData");
                hkaSkeletonMapperData.SetAttributeValue("name", "mapping");
                hkaSkeletonMapperData.SetAttributeValue("signature", "0x3e0a67fd");
                XElement skeletonB = hkaSkeletonMapperData.Elements().FirstOrDefault(hkparam => hkparam.Attribute("name").Value == "skeletonB");
                skeletonB.AddAfterSelf(new XElement("hkparam",
                                       new XAttribute("name", "chainMappingPartitionRanges"),
                                       new XAttribute("numelements", 0)));
                skeletonB.AddAfterSelf(new XElement("hkparam",
                                       new XAttribute("name", "simpleMappingPartitionRanges"),
                                       new XAttribute("numelements", 0)));
                skeletonB.AddAfterSelf(new XElement("hkparam",
                                       new XAttribute("name", "partitionMap"),
                                       new XAttribute("numelements", 0)));
                outputDataSection.Add(hkaSkeletonMapper);
            }

            return outputDataSection;
        }

        static void RenameObjects(XElement xelem, string dataType)
        {
            if (xelem.Parent != null && xelem.Parent.Name == "hksection")
            {
                xelem.Attribute("name").Value = $"#{CurrentName}";
                RenamedObjDict[$"#{CurrentName}"] = xelem;
                CurrentName++;
            }
            
            foreach (XElement childXelem in xelem.Elements())
            {
                // end recursion when reaching the physics data
                if (dataType == "hka" && childXelem.Element("hkparam") != null && childXelem.Element("hkparam").Value == "Physics Scene Data")
                {
                    dataType = "hknp";
                    childXelem.Elements().First(hkparam => hkparam.Attribute("name").Value == "variant").Value = $"#{CurrentName}";
                    childXelem.ElementsAfterSelf().First().Elements().First(hkparam => hkparam.Attribute("name").Value == "variant").Value = $"#{CurrentName + 1}";
                    return;
                }
                else if (!childXelem.HasElements)
                {
                    // remove potential leading whitespace so we can check whether the value is a reference
                    string noWhitespaceValue = string.Join("", childXelem.Value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));

                    if (noWhitespaceValue.StartsWith("#"))
                    {
                        //split value in case it is a list of references
                        string[] refList = childXelem.Value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries);

                        string newRef = "";
                        if (refList.Length > 1)
                        {
                            newRef = "\n";
                        }

                        foreach (string refName in refList)
                        {
                            // set ObjDict based on dataType
                            Dictionary<string, XElement> ObjDict = HkaObjDict;
                            if (dataType == "hknp")
                            {
                                ObjDict = HknpObjDict;
                            }

                            // debug
                            //if (!ObjDict.ContainsKey(refName))
                            //{
                            //    if (RenamedObjDict.ContainsKey(refName))
                            //    {
                            //        continue;
                            //    }
                            //    Console.WriteLine(dataType);
                            //    Console.WriteLine(childXelem.Attribute("name").Value);
                            //    foreach (string r in refList)
                            //    {
                            //        Console.WriteLine(r);
                            //    }
                            //}

                            // check whether object has already been renamed
                            if (ObjDict.ContainsKey(refName) && ObjDict[refName].Attribute("name").Value == refName)
                            {
                                if (refList.Length > 1)
                                {
                                    newRef += $"#{CurrentName}\n";
                                }
                                else
                                {
                                    newRef = $"#{CurrentName}";
                                }
                                RenameObjects(ObjDict[refName], dataType);
                            }
                            else if (ObjDict.ContainsKey(refName) && ObjDict[refName].Attribute("name").Value != refName)
                            {
                                if (refList.Length > 1)
                                {
                                    newRef += ObjDict[refName].Attribute("name").Value + "\n";
                                }
                                else
                                {
                                    newRef = ObjDict[refName].Attribute("name").Value;
                                }
                            }
                            else if (RenamedObjDict.ContainsKey(refName))
                            {
                                if (refList.Length > 1)
                                {
                                    newRef += refName;
                                }
                                else
                                {
                                    newRef = refName;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"referenced object {refName} cannot be found");
                            }
                        }
                        childXelem.Value = newRef;
                    }
                }
                else
                {
                    RenameObjects(childXelem, dataType);
                }
            }
        }
        // converts hknpPhysicsSystemData to hknpRagdollData
        public static void ConvertRagdollData(XElement hknpPhysicsData)
        {
            XElement hknpRagdollData = hknpPhysicsData.Elements().First(hkobj => hkobj.Attribute("class").Value == "hknpPhysicsSystemData");

            hknpRagdollData.Attribute("signature").Value = "0xdc8f20ab";
            hknpRagdollData.Attribute("class").Value = "hknpRagdollData";

            // materials
            foreach (XElement material in hknpRagdollData.Elements().First(hkparam => hkparam.Attribute("name").Value == "materials").Elements())
            {
                material.Elements().First(hkparam => hkparam.Attribute("name").Value == "name").Value = "";

                XElement triggerType = material.Elements().First(hkparam => hkparam.Attribute("name").Value == "triggerVolumeType");
                triggerType.Attribute("name").Value = "triggerType";
                string triggerTypeVal = triggerType.Value.Replace("VOLUME", "TYPE");
                triggerType.Value = triggerTypeVal;

                XElement triggerManifoldTolerance = material.Elements().First(hkparam => hkparam.Attribute("name").Value == "triggerVolumeTolerance");
                triggerManifoldTolerance.Attribute("name").Value = "triggerManifoldTolerance";
                triggerManifoldTolerance.Element("hkobject").Add(new XAttribute("class", "hkUFloat8"), new XAttribute("name", "triggerManifoldTolerance"));

                XElement softContactSeperationVelocity = material.Elements().First(hkparam => hkparam.Attribute("name").Value == "softContactSeperationVelocity");
                softContactSeperationVelocity.Element("hkobject").Add(new XAttribute("class", "hkUFloat8"), new XAttribute("name", "softContactSeperationVelocity"), new XAttribute("signature", "0x7c076f9a"));

                material.Add(new XElement("hkparam", new XAttribute("name", "userData"), 0));
            }

            // motionProperties
            foreach (XElement motionProperty in hknpRagdollData.Elements().First(hkparam => hkparam.Attribute("name").Value == "motionProperties").Elements())
            {
                if (!motionProperty.Elements().Where(hkparam => hkparam.Attribute("name").Value == "timeFactor").Any())
                {
                    motionProperty.Add(new XElement("hkparam", new XAttribute("name", "timeFactor"), "1.0"));
                }
            }

            // bodyCinfos
            Dictionary<string, string> RigidBodyIndices = new();
            XElement[] bodyCinfoArray = hknpRagdollData.Elements().First(hkparam => hkparam.Attribute("name").Value == "bodyCinfos").Elements().ToArray();
            for (int i = 0; i < bodyCinfoArray.Length; i++)
            {
                bodyCinfoArray[i].Add(new XElement("hkparam", new XAttribute("name", "userData"), 0));
                string name = bodyCinfoArray[i].Elements().First(hkparam => hkparam.Attribute("name").Value == "name").Value;
                RigidBodyIndices.Add(name, i.ToString());
            }

            // constraintCinfos
            foreach (XElement constraintCinfo in hknpRagdollData.Elements().First(hkparam => hkparam.Attribute("name").Value == "constraintCinfos").Elements())
            {
                constraintCinfo.Add(new XElement("hkparam", new XAttribute("name", "flags"), 0));
            }

            hknpRagdollData.Elements().First(hkparam => hkparam.Attribute("name").Value == "name").Value = "Default Physics System Data";
            hknpRagdollData.Add(new XElement("hkparam", new XAttribute("name", "skeleton"), "#93"));

            string boneToBodyMap = "";
            foreach (string boneName in RagdollBoneList)
            {
                boneToBodyMap += RigidBodyIndices[boneName] + " ";
            }
            hknpRagdollData.Add(new XElement("hkparam", new XAttribute("name", "boneToBodyMap"), new XAttribute("numelements", RagdollBoneList.Count().ToString()), boneToBodyMap));
        }
    }
}
