using LLMClient.Models;
using System.Text.RegularExpressions;

namespace LLMClient.Services
{
    public class SearchResult
    {
        public Message Message { get; set; } = null!;
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public string HighlightedContent { get; set; } = string.Empty;
        public float SimilarityScore { get; set; } = 0f;
        public bool IsSemanticResult { get; set; } = false;
    }

    public class SemanticSearchResult
    {
        public Message Message { get; set; } = null!;
        public float SimilarityScore { get; set; }
        public string ConversationTitle { get; set; } = string.Empty;
        public DateTime MessageTimestamp { get; set; }
        public bool IsSemanticResult { get; set; } = true;
        public Dictionary<string, object>? SearchMetadata { get; set; }
    }

    public interface ISearchService
    {
        List<SearchResult> SearchInConversation(Conversation conversation, string searchTerm);
        Task<List<SemanticSearchResult>> SemanticSearchAcrossConversationsAsync(List<Conversation> conversations, string query, float minSimilarity = 0.3f, int maxResults = 20);
        Task<List<SearchResult>> SemanticSearchInConversationAsync(Conversation conversation, string query, float minSimilarity = 0.3f);
        string HighlightText(string text, string searchTerm);
        bool HasResults { get; }
        int CurrentResultIndex { get; set; }
        List<SearchResult> CurrentResults { get; }
        SearchResult? GetCurrentResult();
        SearchResult? GetNextResult();
        SearchResult? GetPreviousResult();
        void ClearResults();
    }

    public class SearchService : ISearchService
    {
        private readonly IEmbeddingService? _embeddingService;
        private readonly DatabaseService _databaseService;
        private List<SearchResult> _currentResults = new();
        private int _currentResultIndex = -1;

        public SearchService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
            _embeddingService = null;
        }

        public SearchService(DatabaseService databaseService, IEmbeddingService embeddingService)
        {
            _databaseService = databaseService;
            _embeddingService = embeddingService;
        }

        public bool HasResults => _currentResults.Count > 0;
        public int CurrentResultIndex 
        { 
            get => _currentResultIndex; 
            set 
            { 
                if (value >= 0 && value < _currentResults.Count)
                    _currentResultIndex = value;
            } 
        }
        public List<SearchResult> CurrentResults => _currentResults;

        public List<SearchResult> SearchInConversation(Conversation conversation, string searchTerm)
        {
            _currentResults.Clear();
            _currentResultIndex = -1;

            if (string.IsNullOrWhiteSpace(searchTerm) || conversation?.Messages == null)
                return _currentResults;

            var trimmedSearchTerm = searchTerm.Trim();
            
            foreach (var message in conversation.Messages)
            {
                if (string.IsNullOrWhiteSpace(message.Content))
                    continue;

                // Case-insensitive search
                var matches = Regex.Matches(message.Content, Regex.Escape(trimmedSearchTerm), 
                    RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var result = new SearchResult
                    {
                        Message = message,
                        StartIndex = match.Index,
                        Length = match.Length,
                        HighlightedContent = HighlightText(message.Content, trimmedSearchTerm)
                    };
                    _currentResults.Add(result);
                }
            }

            if (_currentResults.Count > 0)
                _currentResultIndex = 0;

            return _currentResults;
        }

        public string HighlightText(string text, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(searchTerm))
                return text;

            // Simple highlight - replace with styled version
            var pattern = Regex.Escape(searchTerm.Trim());
            return Regex.Replace(text, pattern, $"**{searchTerm.Trim()}**", RegexOptions.IgnoreCase);
        }

        public SearchResult? GetCurrentResult()
        {
            if (!HasResults || _currentResultIndex < 0 || _currentResultIndex >= _currentResults.Count)
                return null;

            return _currentResults[_currentResultIndex];
        }

        public SearchResult? GetNextResult()
        {
            if (!HasResults) return null;

            _currentResultIndex = (_currentResultIndex + 1) % _currentResults.Count;
            return GetCurrentResult();
        }

        public SearchResult? GetPreviousResult()
        {
            if (!HasResults) return null;

            _currentResultIndex = _currentResultIndex <= 0 ? _currentResults.Count - 1 : _currentResultIndex - 1;
            return GetCurrentResult();
        }

        public void ClearResults()
        {
            _currentResults.Clear();
            _currentResultIndex = -1;
        }

        public async Task<List<SemanticSearchResult>> SemanticSearchAcrossConversationsAsync(
            List<Conversation> conversations, 
            string query, 
            float minSimilarity = 0.3f, 
            int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(query) || _embeddingService == null || !_embeddingService.IsInitialized)
                return new List<SemanticSearchResult>();

            try
            {
                // Generuj embedding dla zapytania
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, true);
                if (queryEmbedding == null) return new List<SemanticSearchResult>();

                // Wyszukaj w bazie danych - wykorzystuje już istniejącą metodę
                var results = await _databaseService.SemanticSearchAcrossConversationsAsync(queryEmbedding, minSimilarity, maxResults);
                
                // Przekształć wyniki na SemanticSearchResult
                return results.Select(r => new SemanticSearchResult
                {
                    Message = r.message,
                    SimilarityScore = r.similarity,
                    ConversationTitle = r.conversationTitle,
                    IsSemanticResult = true,
                    SearchMetadata = new Dictionary<string, object>
                    {
                        { "QueryLength", query.Length },
                        { "ModelVersion", _embeddingService.ModelVersion },
                        { "SearchTime", DateTime.UtcNow }
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchService: Semantic search error: {ex.Message}");
                return new List<SemanticSearchResult>();
            }
        }

        public async Task<List<SearchResult>> SemanticSearchInConversationAsync(
            Conversation conversation, 
            string query, 
            float minSimilarity = 0.3f)
        {
            if (string.IsNullOrWhiteSpace(query) || _embeddingService == null || !_embeddingService.IsInitialized || conversation == null)
                return new List<SearchResult>();

            try
            {
                // Generuj embedding dla zapytania
                var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, true);
                if (queryEmbedding == null) return new List<SearchResult>();

                // Wyszukaj w konkretnej konwersacji
                var results = await _databaseService.SemanticSearchInConversationAsync(conversation.Id, queryEmbedding, minSimilarity, 10);
                
                // Przekształć wyniki na SearchResult z semantic flagą
                return results.Select(r => new SearchResult
                {
                    Message = r.message,
                    SimilarityScore = r.similarity,
                    IsSemanticResult = true
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchService: Semantic search in conversation error: {ex.Message}");
                return new List<SearchResult>();
            }
        }
    }
} 