namespace LapTrinhWindows.Services
{
    public interface IPointService
    {
        // Tính điểm tích lũy dựa trên tổng tiền và sản phẩm
        Task<int> CalculatePointsAsync(decimal totalAmount, int productId);
        // Kiểm tra khách hàng có đủ điểm để đổi
        Task<bool> ValidateCustomerPointsAsync(Guid customerId, int pointsRequired);
        // Cập nhật điểm của khách hàng
        Task UpdateCustomerPointsAsync(Guid customerId, int pointsChange);
    }

    // Triển khai dịch vụ tính điểm mặc định
    public class DefaultPointService : IPointService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DefaultPointService> _logger;

        public DefaultPointService(ApplicationDbContext context, ILogger<DefaultPointService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Tính điểm: 1 điểm mỗi 1000 đơn vị tiền
        public Task<int> CalculatePointsAsync(decimal totalAmount, int productId)
        {
            int points = (int)(totalAmount / 1000);
            _logger.LogInformation("Tính được {Points} điểm cho tổng tiền {TotalAmount}", points, totalAmount);
            return Task.FromResult(points);
        }

        // Kiểm tra điểm của khách hàng
        public async Task<bool> ValidateCustomerPointsAsync(Guid customerId, int pointsRequired)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                _logger.LogWarning("Không tìm thấy khách hàng với ID {CustomerId}", customerId);
                return false;
            }
            bool hasEnoughPoints = customer.Points >= pointsRequired;
            if (!hasEnoughPoints)
            {
                _logger.LogWarning("Khách hàng {CustomerId} có {CustomerPoints} điểm, cần {PointsRequired} điểm", 
                    customerId, customer.Points, pointsRequired);
            }
            return hasEnoughPoints;
        }

        // Cập nhật điểm khách hàng
        public async Task UpdateCustomerPointsAsync(Guid customerId, int pointsChange)
        {
            var customer = await _context.Customers.FindAsync(customerId);
            if (customer == null)
            {
                throw new KeyNotFoundException($"Không tìm thấy khách hàng với ID {customerId}.");
            }
            customer.Points += pointsChange;
            if (customer.Points < 0)
            {
                throw new InvalidOperationException("Điểm của khách hàng không thể âm.");
            }
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cập nhật điểm cho khách hàng {CustomerId}: {PointsChange}, tổng mới: {NewPoints}", 
                customerId, pointsChange, customer.Points);
        }
    }
}