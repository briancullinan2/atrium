using System.ComponentModel;
using System.Reflection;

namespace DataLayer.Customization
{



    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum StorageType
    {
        Ephemeral = 0,
        Persistent = 1,
        Remote = 2,
        Test = 3
    }



    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum GradeScale
    {
        Unset = 0,
        AthroughF = 1,
        AthroughFPlusMinus = 2
    }



    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum StepMode { Intro = 0, Video = 1, Quiz = 2, Reward = 3, Investment = 4 }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum DisplayType
    {
        Unset = 0,
        Text = 1,
        Image = 2,
        Video = 3,
        Audio = 4,
        Format = 5
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum ControlMode
    {
        Unset = 0,
        View = 1,
        Edit = 2,
        Owner = 3,
        Group = 4,
        Result = 5,
        List = 6,
        Add = 7,
        Delete = 8,
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum PackMode
    {
        [Description("pack")] // show only one card at a time and return to home
        Card = 1,
        [Description("quiz")] // show a few cards at a time and return to home
        Quiz = 2,
        [Description("funnel")] // show a few cards then go to the next step
        Multi = 3,
        [Description("demo")] // show a few choice cards and go to store page
        Demo = 4,
        [Description("shuffle")] // randomize the cards that are due with subscriptions
        Shuffle = 5
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum CardType
    {
        Unset = 0,
        FlashCard = 1,
        TrueFalse = 2,
        Multiple = 3,
        Short = 4,
        Match = 5,
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum PackStatus : int
    {
        Unset = 0,
        Unpublished = 1,
        Published = 2,
        Public = 3,
        Unlisted = 4,
        Deleted = 5
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum Gender : int
    {
        Female = 1,
        Male = 2,
        Other = 3,
        Unspecified = 0
    }

}
