using mpPInterface;

namespace mpTxtCenter
{
    public class Interface : IPluginInterface
    {
        public string Name => "mpTxtCenter";
        public string AvailCad => "2010";
        public string LName => "Текст по центру";
        public string Description => "Функция позволяет создавать однострочный текст или выравнивать существующий по середине между двумя указанными точками";
        public string Author => "Пекшев Александр aka Modis";
        public string Price => "0";
    }
}