﻿using CrewChiefV4.Audio;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CrewChiefV4.commands
{
    class MacroManager
    {
        public static Boolean stopped = false;

        // make all the macros available so the events can press buttons as they see fit:
        public static Dictionary<string, ExecutableCommandMacro> macros = new Dictionary<string, ExecutableCommandMacro>();

        public static void stop()
        {
            stopped = true;
            KeyPresser.releasePressedKey();
        }

        // This is called immediately after initialising the speech recogniser in MainWindow
        public static void initialise(AudioPlayer audioPlayer, SpeechRecogniser speechRecogniser)
        {
            stopped = false;
            macros.Clear();
            if (UserSettings.GetUserSettings().getBoolean("enable_command_macros"))
            {
                // load the json:
                MacroContainer macroContainer = loadCommands(getUserMacrosFileLocation());
                // if it's valid, load the command sets:
                if (macroContainer.assignments != null && macroContainer.assignments.Length > 0 && macroContainer.macros != null)
                {
                    // get the assignments by game:
                    Dictionary<String, KeyBinding[]> assignmentsByGame = new Dictionary<String, KeyBinding[]>();
                    foreach (Assignment assignment in macroContainer.assignments)
                    {
                        if (!assignmentsByGame.ContainsKey(assignment.gameDefinition))
                        {
                            assignmentsByGame.Add(assignment.gameDefinition, assignment.keyBindings);
                        }
                    }

                    Dictionary<string, ExecutableCommandMacro> voiceTriggeredMacros = new Dictionary<string, ExecutableCommandMacro>();
                    foreach (Macro macro in macroContainer.macros)
                    {
                        Boolean hasCommandForCurrentGame = false;
                        Boolean allowAutomaticTriggering = false;
                        // eagerly load the key bindings for each macro:
                        foreach (CommandSet commandSet in macro.commandSets)
                        {
                            if (commandSet.gameDefinition.Equals(CrewChief.gameDefinition.gameEnum.ToString(), StringComparison.InvariantCultureIgnoreCase))
                            {
                                hasCommandForCurrentGame = true;
                                allowAutomaticTriggering = commandSet.allowAutomaticTriggering;
                                // this does the conversion from key characters to key enums and stores the result to save us doing it every time
                                commandSet.getActionItems(false, assignmentsByGame[commandSet.gameDefinition]);
                                break;
                            }
                        }
                        if (hasCommandForCurrentGame)
                        {
                            // make this macro globally visible:
                            ExecutableCommandMacro commandMacro = new ExecutableCommandMacro(audioPlayer, macro, assignmentsByGame, allowAutomaticTriggering);
                            macros.Add(macro.name, commandMacro);
                            // if there's a voice command, load it into the recogniser:
                            if (macro.voiceTriggers != null && macro.voiceTriggers.Length > 0)
                            {
                                foreach (String voiceTrigger in macro.voiceTriggers)
                                {
                                    if (voiceTriggeredMacros.ContainsKey(voiceTrigger))
                                    {
                                        Console.WriteLine("Voice trigger " + voiceTrigger + " has already been allocated to a different command");
                                    }
                                    else
                                    {
                                        voiceTriggeredMacros.Add(voiceTrigger, commandMacro);
                                    }
                                }
                            }
                        }
                    }
                    try
                    {
                        speechRecogniser.loadMacroVoiceTriggers(voiceTriggeredMacros);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to load command macros into speech recogniser: " + e.Message);
                    }
                }
            }
            else
            {
                Console.WriteLine("Command macros are disabled");
            }
        }

        // file loading boilerplate - needs refactoring
        private static MacroContainer loadCommands(String filename)
        {
            if (filename != null)
            {
                try
                {
                    return JsonConvert.DeserializeObject<MacroContainer>(getFileContents(filename));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error pasing " + filename + ": " + e.Message);
                }
            }
            return new MacroContainer();
        }

        private static String getFileContents(String fullFilePath)
        {
            StringBuilder jsonString = new StringBuilder();
            StreamReader file = null;
            try
            {
                file = new StreamReader(fullFilePath);
                String line;
                while ((line = file.ReadLine()) != null)
                {
                    if (!line.Trim().StartsWith("#"))
                    {
                        jsonString.AppendLine(line);
                    }
                }
                return jsonString.ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading file " + fullFilePath + ": " + e.Message);
            }
            finally
            {
                if (file != null)
                {
                    file.Close();
                }
            }
            return null;
        }

        private static String getUserMacrosFileLocation()
        {
            String path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "saved_command_macros.json");

            if (File.Exists(path))
            {
                Console.WriteLine("Loading user-configured command macros from Documents/CrewChiefV4/ folder");
                return path;
            }
            else
            {
                Console.WriteLine("Loading default command macros from installation folder");
                return Configuration.getDefaultFileLocation("saved_command_macros.json");
            }
        }
    }
}
