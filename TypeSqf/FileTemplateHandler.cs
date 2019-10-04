using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypeSqf.Model;
using TypeSqf.Edit.Services;

namespace TypeSqf.Edit
{
	public static class FileTemplateHandler
	{
		public static FileTemplate[] DefaultFileTemplates
		{
			get
			{
				return new FileTemplate[]
				{
					new FileTemplate("Init File (SQF)", "sqf", @"
// If this machine is the server (hosted or dedicated) then execute the server init file.
if (isServer) then {
	execVM ""Server\InitServer.sqf""
};

// If this machine is a client (player) then execute the client init file.
if (!isDedicated) then {
	execVM ""Client\InitClient.sqf"";
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Description (EXT)", "ext", @"
// Mission Header
class Header
{
	gameType = Coop;
	minPlayers = 1;
	maxPlayers = 10;
};

class Params
{
	class EnemySkill
	{
		// This will be paramsArray[0]
		title = ""Enemy Skill"";
		values[] = {0, 1, 2, 3, 4};
		texts[] = { ""Cadet"", ""Easy"", ""Normal"", ""Hard"", ""Extreme"" };
		default = 2;
	};

	class EnemyFrequency
	{
		// This will be paramsArray[1]
		title=""Enemy Frequency"";
		values[]={1,2,3};
		texts[]={""Few"", ""Some"", ""Many""};
		default = 2;
	};
};

author = ""%AUTHOR%"";

// Load Texts
onLoadName = ""%LOADNAME%"";
onLoadMission = ""%OVERVIEWTEXT%"";

// Preview picture
//loadScreen = ""loadimage.paa"";

// Overview
overviewText = ""%OVERVIEWTEXT%"";
overviewTextLocked = """";

// Overview Image
//overviewPicture = ""overviewimage.paa"";

OnLoadMissionTime = false;

// AI
//disabledAI = 1;
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Script (SQF)", "sqf", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 *
 * Parameter(s):
 * _PARAM1 (TYPE): - DESCRIPTION.
 * _PARAM2 (TYPE): - DESCRIPTION.
 */

scriptName ""%FILENAME%"";

params [""_PARAM1"", ""_PARAM2""];
private [""_VAR1"", ""_VAR2""];
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Class - Empty (SQX)", "sqx", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 */
namespace %NAMESPACE%
{
	public class %FILENAME%
	{
		// Creates a %FILENAME% object.
		public constructor()
		{
		};
	};
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Class - Templated (SQX)", "sqx", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 */
namespace %NAMESPACE%
{
	public class %FILENAME%
	{
		// Field(s):
		// _FIELD1 (TYPE): DESCRIPTION.
		// _FIELD2 (TYPE): DESCRIPTION.
		private fields [""_FIELD1"" as TYPE, ""_FIELD2"" as TYPE];

		// Creates a %FILENAME% object.
		// Parameter(s):
		// _PARAM1 (TYPE): DESCRIPTION.
		public constructor(""_PARAM1"" as TYPE)
		{
			
		};

		// Gets or sets ...
		public property TYPE PROPERTYNAME { get; set; };

		// DESCRIPTION.
		// Parameter(s):
		// _PARAM1 (TYPE): DESCRIPTION.
		// Returns: DESCRIPTION.
		public method TYPE METHODNAME(""_PARAM1"" as TYPE)
		{
			
		};
	};
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Interface - Empty (SQX)", "sqx", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 */
namespace %NAMESPACE%
{
	public interface %FILENAME%
	{
		
	};
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Interface - Templated (SQX)", "sqx", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 */
namespace %NAMESPACE%
{
	public interface %FILENAME%
	{
		// Gets or sets ...
		property TYPE PROPERTYNAME { get; set; };

		// DESCRIPTION.
		// Parameter(s):
		// _PARAM1 (TYPE): DESCRIPTION.
		// Returns: DESCRIPTION.
		method TYPE METHODNAME;
	};
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Function (SQF)", "sqf", @"
/*
 * Name:    %FILENAME%
 * Date:    %DATE%
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * %DESCRIPTION%
 *
 * Parameter(s):
 * _PARAM1 (TYPE): DESCRIPTION.
 * _PARAM2 (TYPE): DESCRIPTION.
 *
 * Returns:
 * %RETURNS%
 */

%FILENAME% = {
	params [""_PARAM1"", ""_PARAM2""];
	private [""_VAR1"", ""_VAR2""];

	// Code here...
};
".Replace("    ", "\t").TrimStart()),
					new FileTemplate("Tank Class - A Working SQX Class Example (SQX)", "sqx", @"
/*
 * Name:    %FILENAME%
 * File:    %FILENAMEFULL%
 * Date:    %DATE% (%TIME%)
 * Version: 1.0
 * Author:  %AUTHOR%
 *
 * Description:
 * Class that models a Tank. The file contains a fully working class (no analyzer or compile errors), as a
 * demo of the SQX class syntax. It is however essentially empty, i.e. its methods do nothing but displaying
 * hints. As a demo, this class contains most of the available SQX features. For a full API Reference, visit
 * www.typesqf.com.
 *
 * You can add, edit and remove these templates. Simply edit the files in the folder 'Templates' located in
 * the Application Data folder. If the 'Templates' folder does not exist, then it, and its content, 
 * will be created when TypeSqf starts.
 */
namespace %NAMESPACE%
{
	public class %FILENAME%
	{
		// Fields:
		// _mTankUnit (Object):  Holds the actual vehicle object.
		// _mSpawnPos (Array): Holds the starting position of the tank.
		private fields[""_mTankUnit"" as Object, ""_mSpawnPos"" as Array];

		// Creates a %FILENAME% object and spawns it with crew on the specified position.
		// Parameter(s):
		// _spawnPos: The position where to spawn the tank.
		public constructor(""_spawnPos"" as Array)
		{
			_mSpawnPos = _spawnPos;
			_mTankUnit = objNull;
			call _self.Respawn;
		};

		// Gets the status of the tank's movement.
		public property String Status { get; private set; };

		// Removes and respawns the tank on its starting position.
		public method Respawn()
		{
			if (!isNull _mTankUnit) then
			{
				{
					deleteVehicle _x;
				} foreach crew _mTankUnit;

				deleteVehicle _mTankUnit;
			};

			_mTankUnit = ""B_MBT_01_cannon_F"" createVehicle _mSpawnPos;
			createVehicleCrew _mTankUnit;
			_self.Status = ""IDLE"";
		};

		// Attacks a position.
		// Parameter(s):
		// _position: The position to attack as an array ([x, y, z]).
		// Returns: true if it is able to carry out an attack, otherwise false.
		public method Boolean AttackPosition()
		{
			params [""_position"" as Array];
			private [""_waypoint""];

			_waypoint = (group _mTankUnit) addWaypoint[_position, 0];
			_waypoint setWaypointType ""SAD"";
			_waypoint setWaypointBehaviour ""COMBAT"";

			_self.Status = ""ATTACKING"";
		};
	};
};
".Replace("    ", "\t").TrimStart()),
				};
			}
		}

		public static void CreateDefaultFileTemplates()
		{
			// Create code templates
			try
			{
				string absoluteTemplateDirectoryName = Path.Combine(CurrentApplication.AppDataFolder, "Templates");
				if (!Directory.Exists(absoluteTemplateDirectoryName))
				{
					Directory.CreateDirectory(absoluteTemplateDirectoryName);
				}

				foreach (FileTemplate template in FileTemplateHandler.DefaultFileTemplates)
				{
					string fullFileName = Path.Combine(absoluteTemplateDirectoryName, template.Name + "." + template.FileExtension);
					File.WriteAllText(fullFileName, template.Content + Environment.NewLine);
				}
			}
			catch
			{
			}
		}
	}
}
