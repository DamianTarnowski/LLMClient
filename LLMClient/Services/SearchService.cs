using LLMClient.Models;
using System.Text.RegularExpressions;

namespace LLMClient.Services
{
    public enum SearchMode
    {
        Vector,     // Wyszukiwanie semantyczne (embeddingi)
        Text,       // Wyszukiwanie tekstowe (regex)
        Hybrid      // Połączenie obu metod
    }

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
        Task<List<SemanticSearchResult>> TextSearchAcrossConversationsAsync(string query, int maxResults = 20);
        Task<List<SemanticSearchResult>> HybridSearchAcrossConversationsAsync(string query, float minSimilarity = 0.3f, int maxResults = 20, float vectorWeight = 0.7f);
        Task<List<SemanticSearchResult>> SearchAsync(string query, SearchMode mode, float minSimilarity = 0.3f, int maxResults = 20, float vectorWeight = 0.7f);
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

        public async Task<List<SemanticSearchResult>> TextSearchAcrossConversationsAsync(
            string query,
            int maxResults = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SemanticSearchResult>();

            try
            {
                var results = await _databaseService.TextSearchAcrossConversationsAsync(query, maxResults);
                
                return results.Select(r => new SemanticSearchResult
                {
                    Message = r.message,
                    SimilarityScore = r.matchScore,
                    ConversationTitle = r.conversationTitle,
                    MessageTimestamp = r.message.Timestamp,
                    IsSemanticResult = false,
                    SearchMetadata = new Dictionary<string, object>
                    {
                        { "SearchType", "Text" },
                        { "QueryLength", query.Length },
                        { "SearchTime", DateTime.UtcNow }
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchService: Text search error: {ex.Message}");
                return new List<SemanticSearchResult>();
            }
        }

        public async Task<List<SemanticSearchResult>> HybridSearchAcrossConversationsAsync(
            string query,
            float minSimilarity = 0.3f,
            int maxResults = 20,
            float vectorWeight = 0.7f)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SemanticSearchResult>();

            try
            {
                var textWeight = 1f - vectorWeight;
                
                // Pobierz wyniki z obu metod równolegle
                var textTask = TextSearchAcrossConversationsAsync(query, maxResults * 2);
                var vectorTask = _embeddingService != null && _embeddingService.IsInitialized
                    ? SemanticSearchAcrossConversationsAsync(new List<Conversation>(), query, minSimilarity, maxResults * 2)
                    : Task.FromResult(new List<SemanticSearchResult>());

                await Task.WhenAll(textTask, vectorTask);

                var textResults = await textTask;
                var vectorResults = await vectorTask;

                // Połącz wyniki - grupuj po MessageId
                var combinedScores = new Dictionary<int, (SemanticSearchResult result, float textScore, float vectorScore)>();

                foreach (var r in textResults)
                {
                    var msgId = r.Message.Id;
                    if (!combinedScores.ContainsKey(msgId))
                        combinedScores[msgId] = (r, r.SimilarityScore, 0f);
                    else
                    {
                        var existing = combinedScores[msgId];
                        combinedScores[msgId] = (existing.result, r.SimilarityScore, existing.vectorScore);
                    }
                }

                foreach (var r in vectorResults)
                {
                    var msgId = r.Message.Id;
                    if (!combinedScores.ContainsKey(msgId))
                        combinedScores[msgId] = (r, 0f, r.SimilarityScore);
                    else
                    {
                        var existing = combinedScores[msgId];
                        combinedScores[msgId] = (existing.result, existing.textScore, r.SimilarityScore);
                    }
                }

                // Oblicz końcowy score hybrydowy
                var hybridResults = combinedScores.Values
                    .Select(x =>
                    {
                        var hybridScore = (x.textScore * textWeight) + (x.vectorScore * vectorWeight);
                        x.result.SimilarityScore = hybridScore;
                        x.result.SearchMetadata = new Dictionary<string, object>
                        {
                            { "SearchType", "Hybrid" },
                            { "TextScore", x.textScore },
                            { "VectorScore", x.vectorScore },
                            { "TextWeight", textWeight },
                            { "VectorWeight", vectorWeight },
                            { "SearchTime", DateTime.UtcNow }
                        };
                        return x.result;
                    })
                    .Where(r => r.SimilarityScore >= minSimilarity * 0.5f) // niższy próg dla hybrydy
                    .OrderByDescending(r => r.SimilarityScore)
                    .Take(maxResults)
                    .ToList();

                return hybridResults;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SearchService: Hybrid search error: {ex.Message}");
                return new List<SemanticSearchResult>();
            }
        }

        public async Task<List<SemanticSearchResult>> SearchAsync(
            string query,
            SearchMode mode,
            float minSimilarity = 0.3f,
            int maxResults = 20,
            float vectorWeight = 0.7f)
        {
            return mode switch
            {
                SearchMode.Text => await TextSearchAcrossConversationsAsync(query, maxResults),
                SearchMode.Hybrid => await HybridSearchAcrossConversationsAsync(query, minSimilarity, maxResults, vectorWeight),
                _ => await SemanticSearchAcrossConversationsAsync(new List<Conversation>(), query, minSimilarity, maxResults)
            };
        }
    }
}