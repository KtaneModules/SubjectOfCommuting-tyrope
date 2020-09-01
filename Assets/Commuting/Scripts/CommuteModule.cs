using KModkit;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class CommuteModule : MonoBehaviour {

    public KMBombInfo BombInfo;
    public KMBombModule Module;
    public KMSelectable[] Buttons;

    private int stage = 0;
    private int stage1solution;
    private bool isActive = false;

    private enum CommuteMethod { walk, cycle, car, bus, train };
    private readonly int numDiffMethods = System.Enum.GetValues(typeof(CommuteMethod)).Length;

    //TODO switch from single-character strings to proper materials.
    private static readonly Dictionary<CommuteMethod, string> materialMap = new Dictionary<CommuteMethod, string>{
        {CommuteMethod.walk,"W" },
        {CommuteMethod.cycle, "C" },
        {CommuteMethod.car, "A" },
        {CommuteMethod.bus, "B" },
        {CommuteMethod.train, "T" }
    };

    #region Generation.
    public void Start() {
        // Assign icons to buttons and send it to the logfile.
        AssignButtons();
        stage = 1;
        isActive = true;
    }

    ///Button has which icon assigned, in reading order.
    private readonly List<CommuteMethod> assignedButtons = new List<CommuteMethod>();

    /// <summary>
    /// This method is called by Start and assigns the icons to the buttons.
    /// </summary>
    /// <returns>A loggable string of assigned buttons.</returns>
    private void AssignButtons() {
        string assignments = "Initialised -- Assigned buttons: ";
        int r;
        for(int i = 0; i < Buttons.Length; i++) {
            r = Random.Range(0, numDiffMethods);

            // Have we already used this one?
            if(assignedButtons.Contains((CommuteMethod) r)) {
                // Yes, try this button again.
                i--;
                continue;
            } else {
                // Nope, this one is available.
                // Register this button.
                assignedButtons.Add((CommuteMethod) r);
                assignments += i.ToString() + ":" + ((CommuteMethod) r).ToString() + ", ";

                // Change the button's physical appearance.
                // TODO Switch from text meshes to some other type of rendering.
                TextMesh buttonText = Buttons[i].GetComponentInChildren<TextMesh>();
                buttonText.text = materialMap[assignedButtons[i]];

                // Don't know why, but we 100% need a local inside-the-loop variable to pass to ButtonPress
                int j = i;
                Buttons[i].OnInteract += delegate () { ButtonPress(j); return false; };
            }
        }
        // Remove the last comma-space
        FormatAndLog(assignments.Substring(0, assignments.Length - 2));
    }

    #endregion

    #region solutionChecking

    /// <summary>
    /// Check which button is correct for Stage 1
    /// </summary>
    /// <returns>Button index, English reading order.</returns>
    private int CheckStage1Solution() {
        // Stage 1
        /*
         * If the bomb has an indicator labelled BOB, take the bus unless the indicator is lit.
         * If the bomb has 3 or more batteries, go by train.
         * If the bomb has a lit indicator labelled CAR, drive to work.
         * If the bomb has a Stereo plug, cycle to work.
         * Walk to work.
        */
        if(BombInfo.IsIndicatorOff("BOB") && assignedButtons.Contains(CommuteMethod.bus)) {
            FormatAndLog("Stage 1: Unlit BOB, go to work by bus. -- Correct: " + assignedButtons.IndexOf(CommuteMethod.bus));
            return assignedButtons.IndexOf(CommuteMethod.bus);
        }
        
        if(BombInfo.GetBatteryCount() > 2 && assignedButtons.Contains(CommuteMethod.train)) {
            FormatAndLog("Stage 1: 3 or more batteries, go to work by train. -- Correct: " + assignedButtons.IndexOf(CommuteMethod.train));
            return assignedButtons.IndexOf(CommuteMethod.train);
        }

        if(BombInfo.IsIndicatorOn("CAR") && assignedButtons.Contains(CommuteMethod.car)) {
            FormatAndLog("Stage 1: lit CAR, drive to work.Button " + assignedButtons.IndexOf(CommuteMethod.car));
            return assignedButtons.IndexOf(CommuteMethod.car);
        }
        
        if(BombInfo.IsPortPresent(Port.StereoRCA) && assignedButtons.Contains(CommuteMethod.cycle)) {
            FormatAndLog("Stage 1: RCA plug, cycle to work. Button " + assignedButtons.IndexOf(CommuteMethod.cycle));
            return assignedButtons.IndexOf(CommuteMethod.cycle);
        }
        
        if(assignedButtons.Contains(CommuteMethod.walk)) {
            FormatAndLog("Stage 1: Walk to work. Button " + assignedButtons.IndexOf(CommuteMethod.walk));
            return assignedButtons.IndexOf(CommuteMethod.walk);
        }

        // None of the above rules apply, go to the IMPORTANT section.
        return CheckImportantSection();
    }

    /// <summary>
    /// Check which button is correct for Stage 2
    /// </summary>
    /// <returns>Button index, English reading order.</returns>
    private int CheckStage2Solution() {
        // Check serial number for the direction.
        List<int> SerialNumerals = BombInfo.GetSerialNumberNumbers().ToList();
        if(SerialNumerals[SerialNumerals.Count - 1] < 6) {
            return CheckStage2Reverse();
        } else {
            return CheckStage2Normal();
        }
    }

    /// <summary>
    /// Helper for CheckStage2Solution -- High Serial Number.
    /// </summary>
    /// <returns>Button index, English reading order.</returns>
    private int CheckStage2Normal() {
        //If you walked to work, cycle home.
        if(assignedButtons.Contains(CommuteMethod.walk)
            && assignedButtons.Contains(CommuteMethod.cycle)
            && stage1solution == assignedButtons.IndexOf(CommuteMethod.walk)) {
            return assignedButtons.IndexOf(CommuteMethod.cycle);
        }

        // If you drove to work, take the car back.
        if(assignedButtons.Contains(CommuteMethod.car)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.car)) {
            return stage1solution;
        }

        //Otherwise, go by train.
        if(assignedButtons.Contains(CommuteMethod.train)) {
            return assignedButtons.IndexOf(CommuteMethod.train);
        }

        // If you took the bus to work, walk home.
        if(assignedButtons.Contains(CommuteMethod.bus)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.bus)
           && assignedButtons.Contains(CommuteMethod.walk)) {
            return assignedButtons.IndexOf(CommuteMethod.walk);
        }

        // If you have a train ticket and there’s more than 5 minutes left on the bomb, ride the bus.
        if(assignedButtons.Contains(CommuteMethod.train)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.train)
           && BombInfo.GetTime() > 300f
           && assignedButtons.Contains(CommuteMethod.bus)) {
            return assignedButtons.IndexOf(CommuteMethod.bus);
        }
        return CheckImportantSection();
    }

    /// <summary>
    /// Helper for CheckStage2Solution -- Low Serial Number.
    /// </summary>
    /// <returns>Button index, English reading order.</returns>
    private int CheckStage2Reverse() {
        // If you have a train ticket and there’s more than 5 minutes left on the bomb, ride the bus.
        if(assignedButtons.Contains(CommuteMethod.train)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.train)
           && BombInfo.GetTime() > 300f
           && assignedButtons.Contains(CommuteMethod.bus)) {
            return assignedButtons.IndexOf(CommuteMethod.bus);
        }
        
        // If you took the bus to work, walk home.
        if(assignedButtons.Contains(CommuteMethod.bus)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.bus)
           && assignedButtons.Contains(CommuteMethod.walk)) {
            return assignedButtons.IndexOf(CommuteMethod.walk);
        }
        
        // If you drove to work, take the car back.
        if(assignedButtons.Contains(CommuteMethod.car) && stage1solution == assignedButtons.IndexOf(CommuteMethod.car)) {
            return stage1solution;
        }
        
        //Otherwise, go by train.
        if(assignedButtons.Contains(CommuteMethod.train)) {
            return assignedButtons.IndexOf(CommuteMethod.train);
        }
        
        //If you walked to work, cycle home.
        if(assignedButtons.Contains(CommuteMethod.walk)
           && assignedButtons.Contains(CommuteMethod.cycle)
           && stage1solution == assignedButtons.IndexOf(CommuteMethod.walk)) {
            return assignedButtons.IndexOf(CommuteMethod.cycle);
        }
        
        return CheckImportantSection();
    }

    /// <summary>
    /// Returns the correct button index to have pressed, assuming the current stage's rules don't apply.
    /// </summary>
    /// <returns></returns>
    private int CheckImportantSection() {
        //Important: If at any point in time none of the rules apply, press the top left* button
        int ret = 0;
        //unless the serial number has a vowel in it, then press the bottom left* button.
        List<char> vowels = new List<char> { 'a', 'e', 'i', 'o', 'u' };
        foreach(char c in BombInfo.GetSerialNumberLetters()) {
            if(vowels.Contains(c)) {
                ret = 2;
            }
        }
        //*) If the amount of minutes remaining is even, use the right button instead.
        int minutes = Mathf.FloorToInt(BombInfo.GetTime() / 60f);
        if(minutes % 2 == 0) {
            ret++;
        }
        FormatAndLog("None of the rules applied, so press button " + ret);
        return ret;
    }

    #endregion

    /// <summary>
    /// This function is called when a button is pressed.
    /// </summary>
    /// <param name="buttonIndex">The position of the pressed button, zero-index, in reading order.</param>
    private void ButtonPress(int buttonIndex ) {
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        GetComponent<KMSelectable>().AddInteractionPunch();

        if(isActive) {
            switch(stage) {
                case 1:
                    stage1solution = CheckStage1Solution();
                    if(buttonIndex == stage1solution) {
                        stage++;
                        FormatAndLog("Correct!");
                        return;
                    }
                    FormatAndLog(buttonIndex + " is not correct. Strike!");
                    Module.HandleStrike();
                    return;
                case 2:
                    int check = CheckStage2Solution();
                    if(check == -1 || buttonIndex == check) {
                        FormatAndLog("Correct!");
                        Module.HandlePass();
                        isActive = false;
                        return;
                    }
                    FormatAndLog(buttonIndex + " is not correct. Strike!");
                    Module.HandleStrike();
                    return;
                default:
                    FormatAndLog("Pressed while active, but not in stages 1 or 2.", true);
                    FormatAndLog("This shouldn't happen, so we'll defuse this module.", true);
                    Module.HandlePass();
                    isActive = false;
                    return;
            }
        }
        FormatAndLog("Pressed while inactive, ignoring.");
    }

    private void FormatAndLog( string log, bool error = false) {
        if(error) {
            Debug.LogError("[Commute]" + log + "\n");
        } else {
            Debug.Log("[Commute]" + log + "\n");
        }
    }
}
