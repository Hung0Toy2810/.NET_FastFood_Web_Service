namespace LapTrinhWindows.Services
{
    // Interface cho việc hash mật khẩu
    public interface IPasswordHasher
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword); // Thêm để kiểm tra sau này
    }

    // Implementation dùng BCrypt
    public class BCryptPasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
                return false;
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}