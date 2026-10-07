using System;
using System.Collections.Generic;

namespace ApplicationLayer.Common
{
    /// <summary>
    /// Standard envelope for all list endpoints (API Spec §3.1). Serializes to:
    /// <c>{ data, pageNumber, pageSize, totalCount, totalPages }</c>.
    /// Construct via <see cref="Create"/> so <see cref="TotalPages"/> stays consistent with the inputs.
    /// </summary>
    /// <typeparam name="T">Element type of the page.</typeparam>
    public sealed class PaginatedResponse<T>
    {
        private PaginatedResponse(IReadOnlyList<T> data, int pageNumber, int pageSize, int totalCount)
        {
            Data = data;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = pageSize > 0
                ? (int)Math.Ceiling(totalCount / (double)pageSize)
                : 0;
        }

        /// <summary>The items belonging to the requested page.</summary>
        public IReadOnlyList<T> Data { get; }

        /// <summary>1-based index of the current page.</summary>
        public int PageNumber { get; }

        /// <summary>Number of items requested per page.</summary>
        public int PageSize { get; }

        /// <summary>Total number of items across all pages (before paging).</summary>
        public int TotalCount { get; }

        /// <summary>Total number of pages, derived from <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
        public int TotalPages { get; }

        /// <summary>True when a page after the current one exists.</summary>
        public bool HasNextPage => PageNumber < TotalPages;

        /// <summary>True when a page before the current one exists.</summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Creates a paginated response. <see cref="TotalPages"/> is computed from the arguments.
        /// </summary>
        /// <param name="data">Items for the current page.</param>
        /// <param name="pageNumber">1-based page index.</param>
        /// <param name="pageSize">Requested page size.</param>
        /// <param name="totalCount">Total item count across all pages.</param>
        public static PaginatedResponse<T> Create(IReadOnlyList<T> data, int pageNumber, int pageSize, int totalCount)
            => new(data, pageNumber, pageSize, totalCount);
    }
}
