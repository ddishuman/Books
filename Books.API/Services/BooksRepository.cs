using Books.API.Contexts;
using Books.API.Entities;
using Books.API.ExternalModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading;

namespace Books.API.Services
{
    public class BooksRepository : IBooksRepository, IDisposable
    {
        private BookContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;

        public BooksRepository(BookContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<BooksRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClientFactory = httpClientFactory ?? 
                throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private Task<int> GetBookPages()
        {
            return Task.Run(() =>
            {
                var pageCalculatr = new Books.Legacy.ComplicatedPageCalculator();
                _logger.LogInformation($"ThreadID when calculating the amount of pages: " +
                    $"{Thread.CurrentThread.ManagedThreadId}");

                return pageCalculatr.CalculateBookPages();
            });
        }

        public async Task<Book?> GetBookAsync(Guid id)
        {
            //var pageCalculatr = new Books.Legacy.ComplicatedPageCalculator();
            //var amountOfPages = pageCalculatr.CalculateBookPages();
            _logger.LogInformation($"ThreadID when entering GetBookAsync: " +
                    $"{Thread.CurrentThread.ManagedThreadId}");

            var bookPages = await GetBookPages();

            return await _context.Books.Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<IEnumerable<Book>> GetBooksAsync()
        {
            await _context.Database.ExecuteSqlAsync($"WAITFOR DELAY '00:00:02';");
            return await _context.Books.Include(b => b.Author).ToListAsync();
        }

        public async Task<IEnumerable<Entities.Book>> GetBooksAsync(
            IEnumerable<Guid> bookIds)
        {
            return await _context.Books.Where(b => bookIds.Contains(b.Id))
                .Include(b => b.Author).ToListAsync();
        }

        public IEnumerable<Book> GetBooks()
        {
            _context.Database.ExecuteSql($"WAITFOR DELAY '00:00:02';");
            return _context.Books.Include(b => b.Author).ToList();
        }

        public async Task<BookCover> GetBookCoverAsync(string coverId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            // pass through a dummy name
            var response = await httpClient
                .GetAsync($"https://localhost:44339/api/bookcovers/{coverId}");
            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<BookCover>(
                    await response.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });
            }

            return null;
        }

        public async Task<IEnumerable<BookCover>> GetBookCoversAsync(Guid bookId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var bookCovers = new List<BookCover>();
            _cancellationTokenSource = new CancellationTokenSource();

            // create a list of fake bookcovers
            var bookCoverUrls = new[]
            {
                $"https://localhost:44339/api/bookcovers/{bookId}-dummycover1",
                //$"https://localhost:44339/api/bookcovers/{bookId}-dummycover2?returnFault=true",
                $"https://localhost:44339/api/bookcovers/{bookId}-dummycover2",
                $"https://localhost:44339/api/bookcovers/{bookId}-dummycover3",
                $"https://localhost:44339/api/bookcovers/{bookId}-dummycover4",
                $"https://localhost:44339/api/bookcovers/{bookId}-dummycover5"
            };

            // create the tasks
            var downloadBookCoverTasksQuery =
                from bookCoverUrl
                in bookCoverUrls
                select DownloadBookCoverAsync(httpClient, bookCoverUrl,
                _cancellationTokenSource.Token);

            // start the tasks
            var downloadBookCoverTasks = downloadBookCoverTasksQuery.ToList();
            try
            {
                return await Task.WhenAll(downloadBookCoverTasks);
            }
            catch (OperationCanceledException operationCanceledException)
            {
                _logger.LogInformation($"{operationCanceledException.Message}");
                foreach (var task in downloadBookCoverTasks)
                {
                    _logger.LogInformation($"Task {task.Id} has status {task.Status}");
                }

                return new List<BookCover>();
            }
            catch (Exception exception)
            {
                _logger.LogError($"{exception.Message}");
                throw;
            }

            

            //foreach (var bookCoverUrl in bookCoverUrls)
            //{
            //    var response = await httpClient.GetAsync(bookCoverUrl);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        bookCovers.Add(JsonSerializer.Deserialize<BookCover>(
            //            await response.Content.ReadAsStringAsync(),
            //            new JsonSerializerOptions
            //            {
            //                PropertyNameCaseInsensitive = true,
            //            }));
            //    }
            //}

            //return bookCovers;
        }

        private async Task<BookCover> DownloadBookCoverAsync(
            HttpClient httpClient, string bookCoverUrl,
            CancellationToken cancellationToken)
        {
            //throw new Exception("Cannot download book cover," +
            //    "writer isn't finishing book fast enough");

            var response = await httpClient.GetAsync(bookCoverUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var bookCover = JsonSerializer.Deserialize<BookCover>(
                    await response.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    });
                return bookCover;
            }

            _cancellationTokenSource.Cancel();
            return null;
        }

        public void AddBook(Book bookToAdd)
        {
            if (bookToAdd == null) 
                throw new ArgumentNullException(nameof(bookToAdd));

            _context.Add(bookToAdd);
        }

        public async Task<bool> SaveChangesAsync()
        {
            // return true if 1 or more entities were changed
            return await _context.SaveChangesAsync() > 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }


    }
}
