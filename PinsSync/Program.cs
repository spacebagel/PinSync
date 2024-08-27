using PinsSync;

internal class Program
{
    private static async Task Main(string[] args)
    {
        File.WriteAllText("pinLinks.txt", string.Empty);
        string boardLink = string.Empty;
        if (args.Length == 0)
        {
            Console.Write("Link to the pin collection: ");
            boardLink = Console.ReadLine();
        }
        else
        {
            boardLink = args[0];
        }
        await PinAlbum.StartPageParser(boardLink);

        if (File.ReadAllText("pinLinks.txt") != string.Empty)
        {
            Console.Write("Do you want to save it? (Y/n) ");
            var saveAnswer = Console.ReadLine();

            if (saveAnswer == "Y" || saveAnswer == "y" || saveAnswer == "yes" || saveAnswer == "Yes")
            {
                await PinAlbum.SaveToFolder();
            }
        }
    }
}