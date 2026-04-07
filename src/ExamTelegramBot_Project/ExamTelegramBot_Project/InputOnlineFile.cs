using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ExamTelegramBot_Project
{
    internal class InputOnlineFile : InputFile
    {
        private FileStream fs;
        private string fileName;

        public InputOnlineFile(FileStream fs, string fileName)
        {
            this.fs = fs;
            this.fileName = fileName;
        }

        public override FileType FileType => throw new NotImplementedException();
    }
}