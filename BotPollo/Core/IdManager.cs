using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    class IdManager
    {
        private int nextId;
        private readonly bool progressive;
        private List<int> UsedIds;

        public IdManager(bool progressive = false)
        {
            if (!progressive)
            {
                UsedIds = new List<int>();
                nextId = new Random().Next(10000, 999999);
                UsedIds.Add(nextId);
            }
            else
            {
                nextId = 0;
            }
            this.progressive = progressive;
        }
        public IdManager(int startId, bool progressive = false)
        {
            if (!progressive)
            {
                UsedIds = new List<int>();
                nextId = new Random().Next(10000, 999999);
                UsedIds.Add(nextId);
            }
            else
            {
                nextId = startId;
            }
            this.progressive = progressive;
        }

        public async Task<int> GetIdAsync()
        {
            return await Task.Run(() =>
            {
                if (progressive)
                {

                    int temp = nextId;
                    nextId++;
                    return temp;
                }
                else
                {
                    int temp = nextId;
                    do
                    {
                        nextId = new Random().Next(10000, 999999);
                    } while (UsedIds.Contains(nextId));
                    return nextId;
                }
            });
        }

        public int GetId()
        {
            if (progressive)
            {

                int temp = nextId;
                nextId++;
                return temp;
            }
            else
            {
                int temp = nextId;
                do
                {
                    nextId = new Random().Next(10000, 999999);
                } while (UsedIds.Contains(nextId));
                return nextId;
            }
        }
    }
}
