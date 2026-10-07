namespace invetoryBackGroundServices.Params
{
    public class GetMachineInfoReques
    {
        public string  Ip { get; set; }
        public string Port { get; set; }
    
    }


    public class LoadCardRequest
    {
        public string Ip { get; set; }
        public string Port { get; set; }
        public int FeederId { get; set; }

    }
    public class EjectCardReq
    {
        public string Ip { get; set; }
        public string Port { get; set; }
        public int HooperId { get; set; }

    }
}
