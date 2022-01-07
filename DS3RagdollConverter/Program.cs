using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
                XElement hknpDataSection = GetHknpPhysics();
                XElement hkaDataSection2014 = ConvertHka();

                BuildObjDict(hknpDataSection, "hknp");
                BuildObjDict(hkaDataSection2014, "hka");

                RenameObjects(hkaDataSection2014, "hka");
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
                OutputRoot.Save(args[0]);
            }
            
        }

        public static XElement InputRoot { get; set; }

        public static XElement OutputRoot { get; set; }

        public static Dictionary<string, XElement> HkaObjDict { get; set; }

        public static Dictionary<string, XElement> HknpObjDict { get; set; }

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
            OutputRoot.Add(hkaData);
            hkaData.Add(hknpData.Elements());
            hkaData.Elements().OrderBy(e => int.Parse(e.Attribute("Name").Value.Substring(1)));
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
            List<XElement> namedVariants = hkRootLevelElement.Element("hkparam").Elements().ToList();
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

            string[] args = { baseDirectory + "temp/hkpPhysics.xml", baseDirectory + "temp/hknpPhysics.xml" };
            Process.Start("Dependencies/hkp2hknp.exe", args);
            XElement hkpackfileHknpPhysics = XElement.Load("temp/hknpPhysics.xml");

            Directory.Delete(baseDirectory + "temp");

            XElement hknpPhysicsData = hkpackfileHknpPhysics.Element("hksection");

            return hknpPhysicsData;
        }

        // converts 2010 hka data to 2014 hka format, returns an hkRootLevelContainer
        static XElement ConvertHka()
        {
            XElement inputDataSection = InputRoot.Elements("hksection").FirstOrDefault(hksection => hksection.Attribute("name").Value == "__data__");
            XElement outputDataSection = new XElement("hksection", new XAttribute("name", "__data__"));

            // creates a copy of the hkRootLevelContainer from the input file, adjusts the ragdoll instance variant to match the ragdoll data variant found in the 2014 version and adds it to the output __data__ hksection
            XElement inputHkRootLevelContainer = inputDataSection.Elements().FirstOrDefault(hkobj => hkobj.Attribute("name").Value == InputRoot.Attribute("toplevelobject").Value);
            XElement outputHkRootLevelContainer = new XElement(inputHkRootLevelContainer);
            XElement ragdollIns = outputHkRootLevelContainer.Element("hkparam").Elements()
                                  .FirstOrDefault(hkobj => hkobj.Value.Contains("hkaRagdollInstance"));
            ragdollIns.Elements().FirstOrDefault(hkparam => hkparam.Value == "hkaRagdollInstance").Value = "hknpRagdollData";
            ragdollIns.Elements().FirstOrDefault(hkparam => hkparam.Value == "RagdollInstance").Value = "Physics Ragdoll";
            outputDataSection.Add(outputHkRootLevelContainer);

            // converts the hkaAnimationContainer including its hkaSkeleton children to the 2014 format and adds them to the output __data__ hksection
            XElement hkaAnimationContainer = new XElement(inputDataSection.Elements().FirstOrDefault(hkobj => hkobj.Attribute("class").Value == "hkaAnimationContainer"));
            hkaAnimationContainer.Attribute("signature").Value = "0x26859f4c";
            outputDataSection.Add(hkaAnimationContainer);

            foreach (XElement hkaSkeleton in inputDataSection.Elements().Where(hkobj => hkobj.Attribute("class").Value == "hkaSkeleton"))
            {
                hkaSkeleton.Attribute("signature").Value = "0xfec1cedb";

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
            }

            return outputDataSection;
        }

        static void RenameObjects(XElement xelem, string dataType)
        {
            if (xelem.Parent.Name == "hksection")
            {
                xelem.Attribute("name").Value = $"#{CurrentName}";
                CurrentName++;
            }
            
            foreach (XElement childXelem in xelem.Elements())
            {
                // remove potential leading whitespace so we can check whether the value is a reference
                string noWhitespaceValue = string.Join("", childXelem.Value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));

                // end recursion when reaching the physics data
                if (noWhitespaceValue == "Physics Data")
                {
                    dataType = "hknp";
                    xelem.Elements().First(hkparam => hkparam.Attribute("name").Value == "variant").Value = $"#{CurrentName}";
                    xelem.ElementsAfterSelf().First().Elements().First(hkparam => hkparam.Attribute("name").Value == "variant").Value = $"#{CurrentName+1}";
                }
                else if (noWhitespaceValue.StartsWith("#"))
                {
                    //split value in case it is a list of references
                    string[] refList = xelem.Value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries);

                    string newRef = "";
                    if (refList.Length > 1)
                    {
                        newRef = "\n";
                    }
                    
                    foreach (string refName in refList)
                    {
                        Dictionary<string, XElement> ObjDict = HkaObjDict;
                        if (dataType == "hknp")
                        {
                            ObjDict = HknpObjDict;
                        }

                        // check whether object has already been renamed
                        if (ObjDict[refName].Attribute("name").Value == refName)
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
                        else
                        {
                            newRef += ObjDict[refName].Attribute("name").Value;
                        }
                    }
                    childXelem.Value = newRef;
                }
                else
                {
                    RenameObjects(childXelem, dataType);
                }
            }
        }
    }
}
