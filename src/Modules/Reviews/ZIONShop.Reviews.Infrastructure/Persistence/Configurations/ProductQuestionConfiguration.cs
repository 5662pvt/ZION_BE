using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Infrastructure.Persistence.Configurations;

public class ProductQuestionConfiguration : IEntityTypeConfiguration<ProductQuestion>
{
    public void Configure(EntityTypeBuilder<ProductQuestion> builder)
    {
        builder.ToTable("ProductQuestions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.ProductId).IsRequired();
        builder.Property(q => q.UserId).IsRequired();
        builder.Property(q => q.AuthorName).HasMaxLength(256).IsRequired();
        builder.Property(q => q.Content).HasMaxLength(1000).IsRequired();
        builder.Property(q => q.AnswerContent).HasMaxLength(2000);
        builder.Property(q => q.AnsweredAt);
        builder.Property(q => q.AnsweredByUserId);

        builder.Property(q => q.CreatedDate).IsRequired();
        builder.Property(q => q.CreatedBy).HasMaxLength(128);
        builder.Property(q => q.LastModifiedDate);
        builder.Property(q => q.LastModifiedBy).HasMaxLength(128);
        builder.Property(q => q.IsDeleted).HasDefaultValue(false);
        builder.Ignore(q => q.RowVersion);
        builder.Ignore(q => q.DomainEvents);
        builder.HasQueryFilter(q => !q.IsDeleted);

        builder.HasIndex(q => new { q.ProductId, q.CreatedDate });
    }
}
