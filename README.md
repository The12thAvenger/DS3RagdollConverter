# DS3RagdollConverter
Tool for converting Havok 2010 ragdoll XML files to the Havok 2014 format for use with Dark Souls 3. Can be used to create your own ragdolls, also supports porting Dark Souls 1 ragdolls to Dark Souls 3.

## Instructions
* Create a ragdoll file using the 2010 32-bit havok content tools plugin for 3ds Max. See the included manual for instructions on how to do that (you will find it under Havok Content Tools -> Help -> Content Tools Help). 
* Export using the ragdoll preset which comes with the plugin. I usually set it to build rig from selected in the "Create Skeletons" filter and select only the animation skeleton, not the ragdoll, which will be detected automatically. Make sure to set it to export an XML packfile in the "Write to Platform" filter.
* Drag and drop the exported XML file onto DS3RagdollConverter.exe
* Use hkxpack-souls to convert the XML file into an hkx packfile.

Special thanks to [Horkrux](https://github.com/horkrux) for hkp2hknp
