using DomainLayer.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Entities
{
    public class Stock:AuditableEntity
    {

        [ForeignKey(nameof(Bank))]
        public long  TenantId { get; set; }
        public Tenant Bank { get; set; }
        [ForeignKey(nameof(SettledBranch))]
        public long BranchId { get; set; }
        public Branch SettledBranch { get; set; }
        [ForeignKey(nameof(CardType))]
        public long ProductId { get; set; }
        public Product CardType { get; set; }
        public int AvailableQuantity { get; set; } = 0;
        public int HoldQuantity { get; set; } = 0;
        [Timestamp]
        public byte[] RowVersion { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
