# STAR Mentor System

System for Telementoring with Augmented Reality. Mentor subsystem.

* Homepage: <https://engineering.purdue.edu/starproj/>
* Mentee subsystem: <https://github.com/practisebody/STAR>
* STAR Controller App: <https://www.github.com/practisebody/STARController_UWP>

## Installing

### Miscellaneous

* The system was tested in two different Windows 10 PCs with in-built touch capabilities, and on three different Windows 10 PCs connected to a touch disply.
* The installation time of the Mentor System is of less than 5 minutes. When combined, the installation time of the entire STAR platform is less than 15 minutes.

### Prerequisites

A computer with Windows 10, version 10.0.17763 or equivalent. Additionally, PC requires a touch display (built-in or connected to one). If the PC used for this system does not have a touch display, the "Draw Lines" functionality will not properly work. 

### Getting Started

1. Get the app
	1. On the PC, download the latest release at <https://github.com/edkazar/MentorSystemUWPWebRTC/releases>
	2. Unzip MentorSystemWebRTC.zip
2. Install Dependencies
	1. Go to the Dependencies/x86 folder
	2. Locate and install all .appx files in that folder
		1. Your computer might have these dependencies installed already.
	2. Go back to the main folder and locate the Security Certificate (.cer)
		1. Click on Install Certificate
		2. Select Local Machine (you might require admin permission of this step)
		3. Select "Place all certificates in the following store" and click on Browse
		4. Select the "Trusted People" folder and click OK
		5. Click Next, then Finish
3. Locate and install the .appxbundle file
	1. This will install the software as a Windows 10 app.

## Compiling

It is not recommended to compile from scratch. Follow the steps below when necessary.

### Prerequisites

* Unity (2017.4.3f1 or later, but no later than 2018)
* Visual Studio (2017, 15.9.9 or later), with the following components
	* Universal Windows Platform development
	* .NET Capabilities
	* Game development with Unity

### Getting Started

1. Download or "git clone" this repository
	git clone https://github.com/edkazar/MentorSystemUWPWebRTC.git
2. Build Visual Studio project by Unity
	1. Use Unity to open the project. *Note: The first time to open the project may take a while load all the files*
	2. Open "Scenes/MentorSystemWebRTC" from project window
	3. Click on "File > Build Settings", choose "Universal Windows Platform" and click "Switch Platform"
	4. Check "Debugging > Unity C# Projects" and close the window
	5. If using later unity version, check "Edit > Project Settings > Player > Other Settings > Allow unsafe code". *Note: this option is not available in 2017.4.3f1*
	6. If using later unity version, check "Edit > Project Settings > Player > Publishing Settings. Under the Certificate option, click on "Create..." to renew the project WSA Certificate.
	7. Click on "Mixed Reality Toolkit > Build Window", click "Build Unity Project"
	8. We recommend deleting the "manifest.json" file in the MentorSystemUWPWebRTC/Packages folder to prevent compilation errors. 
	9. Click on "File > Build Settings" > Build. In the File Explorer, create a folder named App and select it. Afterwards, click "Select Folder"
3. Run app by Visual Studio
	1. Open "MentorSystemUWPWebRTC/App/MentorSystemWebRTC/.sln" using Visual Studio
	2. In Configuration Manager, switch to "Release" and "x86". Select "Local Machine"
	3. In Solution Explorer, right click on "Package.appxmanifest", and select View Code.
	4. Below the < /Capabilities> section, add the following segment (make sure to remote the . after the <): 

&emsp;&emsp;&ensp;<.Extensions><br>
&emsp;&emsp;&ensp;&ensp;&emsp;&emsp;<.Extension Category="windows.activatableClass.inProcessServer"><br>
&emsp;&emsp;&ensp;&ensp;&ensp;&emsp;&emsp;&emsp;&emsp;<.InProcessServer><br>
&emsp;&emsp;&ensp;&ensp;&ensp;&ensp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;<.Path>WebRtcScheme.dll</Path><br>
&emsp;&emsp;&ensp;&ensp;&ensp;&ensp;&emsp;&emsp;&emsp;&emsp;&emsp;&emsp;<.ActivatableClass ActivatableClassId="WebRtcScheme.SchemeHandler" ThreadingModel="both"/<br>
&emsp;&emsp;&ensp;&ensp;&ensp;&emsp;&emsp;&emsp;&emsp;<./InProcessServer><br>
&emsp;&emsp;&ensp;&ensp;&emsp;&emsp;<./Extension><br>
&emsp;&emsp;&ensp;<./Extensions>

	5. Click "Debug > Start Debugging"



