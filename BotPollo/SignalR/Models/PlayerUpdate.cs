using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;

namespace BotPollo.SignalR.Models
{
    public class PlayerUpdate
    {
        public bool isPlaying;
        public string url;
        public string id;
        public string title;
        public Author author;
        public Container containerType;
        public Bitrate bitrate; //Kbps
        public TimeSpan? duration;
        public TimeSpan position;
        public IReadOnlyList<Thumbnail> thumbnails;

        public PlayerUpdate(bool isPlaying, string url, string id, string title, Author author, Container containerType, Bitrate bitrate, TimeSpan? duration, TimeSpan position, IReadOnlyList<Thumbnail> thumbnails)
        {
            this.isPlaying = isPlaying;
            this.url = url;
            this.id = id;
            this.title = title;
            this.author = author;
            this.containerType = containerType;
            this.bitrate = bitrate;
            this.duration = duration;
            this.position = position;
            this.thumbnails = thumbnails;
        }
    }
}
