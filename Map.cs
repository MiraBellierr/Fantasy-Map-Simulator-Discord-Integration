using MelonLoader;
using HarmonyLib;
using UnityEngine;
using System.IO;
using Il2Cpp;
using System;
using System.Text.RegularExpressions;
using System.Reflection;

public class Map : MelonMod
{
    private const string QueueFilePath = @"C:/data/commands_queue.txt";
    private const string FeedbackFilePath = @"C:/data/feedback.txt";
    private System.Collections.Generic.Queue<string> commandQueue = new System.Collections.Generic.Queue<string>();
    private readonly System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
    private readonly System.Random random = new System.Random();
    private float lastCheckTime;
    private const float CheckInterval = 5f;

    public override void OnInitializeMelon()
    {
        HarmonyInstance.PatchAll(MelonAssembly.Assembly);
        MelonLogger.Msg("Game loaded.");
    }

    public override void OnUpdate()
    {
        if (Time.time - lastCheckTime > CheckInterval)
        {
            lastCheckTime = Time.time;
            ProcessCommandsFromFile();
        }

        while (commandQueue.Count > 0)
        {
            ProcessCommand(commandQueue.Dequeue());
        }
    }

    private void SendFeedback(string feedback)
    {
        File.AppendAllText(FeedbackFilePath, feedback + Environment.NewLine);
        MelonLogger.Msg(feedback);
    }

    private bool IsStateNameTaken(string name)
    {
        foreach (State state in StateManager.Instance.states)
        {
            if (state.Name == name)
                return true;
        }
        return false;
    }

    private void RegisterName(string name)
    {

        if (names.Contains(name))
        {
            SendFeedback($"{name} has been registered");
            return;
        }

        Il2CppSystem.Collections.Generic.List<State> states = StateManager.Instance.states;
        int numberOfCountries = states.Count;
        bool nameAssigned;

        while (true)
        {
            int randomIndex = random.Next(1 +  numberOfCountries + 1);

            if (!names.Contains(states[randomIndex].Name))
            {
                states[randomIndex].Name = name;
                StateManager.Instance.ForceUpdateStateName();
                names.Add(name);
                nameAssigned = true;
                break;
            }
        }

        if (nameAssigned)
        {
            SendFeedback($"{name} has been registered");
        } 
        else
        {
            SendFeedback($"Sorry, no countries to be assigned for you - ${name}");
        }
    }


    private void ProcessCommandsFromFile()
    {
        if (File.Exists(QueueFilePath))
        {
            commandQueue = new System.Collections.Generic.Queue<string>(File.ReadAllLines(QueueFilePath));
            File.WriteAllText(QueueFilePath, string.Empty);
        }
    }

    private void ProcessCommand(string command)
    {
        string[] parts = command.Split(' ');

        switch (parts[0])
        {
            case "start":
                World.Instance.GenerateMap();
                names.Clear();
                SendFeedback("I have generated a new map for you.");
                break;

            case "register":
                string userName = parts[1];
                if (IsStateNameTaken(userName))
                {
                    SendFeedback($"User '{userName}' is already registered.");
                }
                else
                {
                    RegisterName(userName);
                }
                break;

            case "info":
                ShowStateInfo(parts[1]);
                break;

            default:
                if (parts.Length == 3)
                    HandleMethodInvocation(parts[0], parts[1], parts[2]);
                else
                    MelonLogger.Msg($"Invalid command format: {command}");
                break;
        }
    }

    private void ShowStateInfo(string userName)
    {
        Il2CppSystem.Collections.Generic.List<State> states = StateManager.Instance.states;
        State state = null;

        foreach (State s in states)
        {
            if (s.Name == userName)
            {
                state = s;
                break;
            }
        }

        if (state == null) SendFeedback($"there is no {userName} country.");
        else
        {
            Il2CppSystem.Collections.Generic.HashSet<State> enemies = state.GetEnemyStates();
            System.Collections.Generic.List<string> enemyNames = new System.Collections.Generic.List<string>();

            foreach (State enemy in enemies)
            {
                enemyNames.Add(enemy.Name);
            }
            string enemiesAsString = string.Join(", ", enemyNames);

            Il2CppSystem.Collections.Generic.List<Trait> traits = state.traits;
            System.Collections.Generic.List<string> userTraits = new System.Collections.Generic.List<string>();

            foreach (Trait trait in traits)
            {
                userTraits.Add(trait.name);
            }

            string traitsAsString = string.Join(", ", userTraits);

            SendFeedback($"Name: {state.GetFullName()}-Color: {state.colorString}-Ethnic: {(EthnicAttribute)state.ethnicAttribute}-" +
                $"Diplomacy Power: {state.diplomacyPower}-Diplomacy Efficiency: {state.diplomacyefficiency}-" +
                $"Food Reserve: {state.foodReserve}-Population: {state.GetTotalPopulation()}-Gold: {state.gold}" +
                $"-Gold/Month: {state.goldPerMonth}-Political System: {(PoliticalSystem)state.politicalSystem}-" +
                $"traits: {traitsAsString}-Enemies: {enemiesAsString}");
        }
    }



private void HandleMethodInvocation(string userName, string methodName, string targetName)
    {
        State userState = null;
        State targetState = null;

        foreach (State state in StateManager.Instance.states)
        {
            if (state.GetUIName() == userName)
                userState = state;
            if (state.GetUIName() == targetName)
                targetState = state;

            if (userState != null && targetState != null)
                break;
        }

        if (userState != null && targetState != null)
        {
            MethodInfo method = typeof(State).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            if (method != null)
            {
                method.Invoke(userState, new object[] { targetState });
                MelonLogger.Msg($"Method '{methodName}' successfully invoked on {userState.GetUIName()} with {targetState.GetUIName()} for command: {userName} {methodName} {targetName}");
            }
            else
            {
                MelonLogger.Msg($"Method '{methodName}' not found on {userState.GetUIName()} for command: {userName} {methodName} {targetName}");
            }
        }
        else
        {
            MelonLogger.Msg($"One or both states not found for command: {userName} {methodName} {targetName}");
            SendFeedback($"{userName} or {targetName} not found.");
        }
    }

    [HarmonyPatch(typeof(Il2Cpp.HistoryManager), "RecordEvent")]
    private static class WarEvent
    {
        private static void Postfix(Il2Cpp.HistoryEvent e)
        {
            File.AppendAllText(FeedbackFilePath, CleanMessage(e.GetDescription()) + Environment.NewLine);
        }

        private static string CleanMessage(string input)
        {
            return Regex.Replace(input, "<.*?>", string.Empty);
        }
    }
}
