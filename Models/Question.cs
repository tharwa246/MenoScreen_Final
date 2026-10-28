namespace MenoScreen.Models;

public enum QuestionType
{
    Context,
    Scored,
    SafetyFlag
}
public class Question
{
    public int Id {get;set;}
    public int QuestionSetVersionId {get;set;}
    public QuestionSetVersion QuestionSetVersion {get;set;} = null!;


    public int Number {get;set;} //Displays the order of the question in the questionnaire. It is not necessarily the same as SortOrder, which is used to sort questions within a block.
    public int SortOrder {get;set;}
    public string Block {get;set;}=string.Empty; //example "Cognitive & Mood".
    public string Text {get;set;}=string.Empty;
    public QuestionType Type {get;set;} 
    public string? Options {get;set;} //patient context questions only. 
}