namespace TodoApp.Configuration
{
    public class AuthResult
    {
        public string Token { get; set; }
        public bool Success { get; set; }
        public List<string> Error { get; set; }

        public string RefreshToken {  get; set; }
    }
}
