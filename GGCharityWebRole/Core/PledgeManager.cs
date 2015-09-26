using GGCharityData;
using GGCharityWebRole.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Threading.Tasks;

namespace GGCharityWebRole
{
    public class PledgeManager
    {
        GGCharityWebDatabase _context;
        DateTime _effectiveTime;

        protected PledgeManager(GGCharityWebDatabase data, DateTime effectiveTime)
        {
            _context = data;
            _effectiveTime = effectiveTime;
        }

        public static PledgeManager Get(GGCharityWebDatabase data, DateTime effectiveTime)
        {
            return new PledgeManager(data, effectiveTime);
        }

        public Pledge AddPledge(EventRegistration PlayerRegistration, GGCharityUser donor, Decimal Amount)
        {
            var pledge = new Pledge(PlayerRegistration, donor, Amount);
            _context.Pledges.Add(pledge);
            return pledge;
        }

        public async Task RemovePledgeAsync(EventRegistration registration, GGCharityUser Donor)
        {
            var pledge = await (from p in _context.Pledges 
                                where p.Receiver.UserId.Equals(registration.UserId) && p.Receiver.EventId.Equals(registration.EventId) && p.DonorId.Equals(Donor.Id) 
                                select p).SingleOrDefaultAsync();

            _context.Pledges.Remove(pledge);
        }

        public async Task<Pledge> FindDonorPledgeAsync(string ViewerId, string ReceiverId, int EventId)
        {
            var pledge = await (from p in _context.Pledges
                                where p.Receiver.UserId.Equals(ReceiverId) && p.DonorId.Equals(ViewerId) && p.Receiver.EventId.Equals(EventId)
                                select p).SingleOrDefaultAsync();
            return pledge;
        }

        internal async Task<List<Pledge>> FindPledgesFromDonorForEventAsync(string DonorId, int EventId, params string[] includes)
        {
            return await (from p in _context.Pledges 
                          where p.DonorId.Equals(DonorId) && p.EventId.Equals(EventId)
                          select p).AddIncludes(includes).ToListAsync();
        }

        internal async Task<List<Pledge>> FindPledgesForPlayerAsync(string ReceiverId, int EventId)
        {
            return await (from p in _context.Pledges
                          where p.Receiver.UserId.Equals(ReceiverId) && p.EventId.Equals(EventId)
                          select p).ToListAsync();
        }

        internal async Task<List<IGrouping<Charity, Pledge>>> FindPledgesFromDonorByCharityAsync(string DonorId, int EventId)
        {
            var result = await (from p in _context.Pledges
                                where p.Donor.Id.Equals(DonorId) && p.EventId.Equals(EventId)
                                group p by p.Receiver.Charity into pledgesByCharity
                                select pledgesByCharity).ToListAsync();

            return result;
        }

        internal async Task SetCompletedPledgesForCharityAsync(string DonorUserName, int EventId, int CharityId, bool Completed)
        {
            var pledgesToCharity = await (from p in _context.Pledges
                                          where p.Receiver.Charity.Id.Equals(CharityId) 
                                                && p.Donor.UserName.Equals(DonorUserName) 
                                                && p.EventId.Equals(EventId) 
                                                && !p.PledgeCompleted.Equals(Completed)
                                          select p).ToListAsync();
            
            foreach(var pledge in pledgesToCharity)
            {
                pledge.PledgeCompleted = Completed;
            }
        }
    }
}