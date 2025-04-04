namespace LapTrinhWindows.DataSeeding
{
    class SeedData
    {
        public static void InitializeData(ApplicationDbContext context)
        {
            context.Database.EnsureCreated();
            if (!context.Customers.Any())
            {
                // Danh sách khách hàng cần thêm
                var customers = new List<Customer>
                {
                    new Customer { CustomerName = "Nguyễn Văn A", Address = "Hà Nội", PhoneNumber = "0123456789", HashPassword = "password123" },
                    new Customer { CustomerName = "Trần Thị B", Address = "TP Hồ Chí Minh", PhoneNumber = "0987654321", HashPassword = "password456" },
                    new Customer { CustomerName = "Lê Văn C", Address = "Đà Nẵng", PhoneNumber = "0345678912", HashPassword = "password789" },
                    new Customer { CustomerName = "Hoàng Thị D", Address = "Cần Thơ", PhoneNumber = "0765432198", HashPassword = "password321" },
                    new Customer { CustomerName = "Phạm Văn E", Address = "Hải Phòng", PhoneNumber = "0654321987", HashPassword = "password654" },
                    new Customer { CustomerName = "Vũ Văn F", Address = "Huế", PhoneNumber = "0456789123", HashPassword = "password987" },
                    new Customer { CustomerName = "Đỗ Thị G", Address = "Nha Trang", PhoneNumber = "0789456123", HashPassword = "password555" },
                    new Customer { CustomerName = "Bùi Văn H", Address = "Quảng Ninh", PhoneNumber = "0888888888", HashPassword = "password111" },
                    new Customer { CustomerName = "Ngô Thị I", Address = "Bắc Ninh", PhoneNumber = "0999999999", HashPassword = "password222" },
                    new Customer { CustomerName = "Dương Văn K", Address = "Vũng Tàu", PhoneNumber = "0666666666", HashPassword = "password333" }
                };
                context.Customers.AddRange(customers);
                context.SaveChanges();

            }



            if (!context.EmployeeRoles.Any())
            {
                var roles = new List<EmployeeRole>
                {
                    new EmployeeRole { RoleName = "Nhân viên" },
                    new EmployeeRole { RoleName = "Quản lý" }
                };

                context.EmployeeRoles.AddRange(roles);
                context.SaveChanges();
            }
            var employeeRoles = context.EmployeeRoles.ToList();
            var roleNhanVien = employeeRoles.First(r => r.RoleName == "Nhân viên").RoleID;
            var roleQuanLy = employeeRoles.First(r => r.RoleName == "Quản lý").RoleID;

            if (!context.Employees.Any())
            {
                var employees = new List<Employee>
                {
                    new Employee { FullName = "Nguyễn Văn A", Address = "Hà Nội", PhoneNumber = "0123456789", Email = "a@example.com", HashPassword = "password123", RoleID = roleNhanVien, Status = EmployeeStatus.Offline },
                    new Employee { FullName = "Trần Thị B", Address = "TP Hồ Chí Minh", PhoneNumber = "0987654321", Email = "b@example.com", HashPassword = "password456", RoleID = roleNhanVien, Status = EmployeeStatus.Online },
                    new Employee { FullName = "Lê Văn C", Address = "Đà Nẵng", PhoneNumber = "0345678901", Email = "c@example.com", HashPassword = "password789", RoleID = roleNhanVien, Status = EmployeeStatus.Offline },
                    new Employee { FullName = "Phạm Thị D", Address = "Cần Thơ", PhoneNumber = "0765432109", Email = "d@example.com", HashPassword = "password321", RoleID = roleNhanVien, Status = EmployeeStatus.Online },
                    new Employee { FullName = "Hoàng Văn E", Address = "Hải Phòng", PhoneNumber = "0981234567", Email = "e@example.com", HashPassword = "password654", RoleID = roleQuanLy, Status = EmployeeStatus.Online }
                };

                context.Employees.AddRange(employees);
                context.SaveChanges();
            }
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { CategoryName = "Đồ uống" },
                    new Category { CategoryName = "Thức ăn nhanh" },
                    new Category { CategoryName = "Tráng miệng" },
                    new Category { CategoryName = "Đồ ăn chay" }
                };

                context.Categories.AddRange(categories);
                context.SaveChanges();
            }
            if (!context.Products.Any())
            {
                var categories = context.Categories.ToList();

                var products = new List<Product>
                {
                    new Product { ProductName = "Trà Sữa", CategoryID = categories[0].CategoryID, Price = 35000, AvailableQuantity = 100, Discount = 0.1 },
                    new Product { ProductName = "Cà Phê Đen", CategoryID = categories[0].CategoryID, Price = 25000, AvailableQuantity = 50, Discount = 0.05 },
                    new Product { ProductName = "Hamburger", CategoryID = categories[1].CategoryID, Price = 50000, AvailableQuantity = 70, Discount = 0.15 },
                    new Product { ProductName = "Khoai Tây Chiên", CategoryID = categories[1].CategoryID, Price = 30000, AvailableQuantity = 120, Discount = 0.1 },
                    new Product { ProductName = "Bánh Ngọt", CategoryID = categories[2].CategoryID, Price = 40000, AvailableQuantity = 80, Discount = 0.2 },
                    new Product { ProductName = "Kem Dâu", CategoryID = categories[2].CategoryID, Price = 45000, AvailableQuantity = 60, Discount = 0.1 },
                    new Product { ProductName = "Salad Rau Củ", CategoryID = categories[3].CategoryID, Price = 55000, AvailableQuantity = 90, Discount = 0.05 },
                    new Product { ProductName = "Đậu Hũ Chiên", CategoryID = categories[3].CategoryID, Price = 28000, AvailableQuantity = 100, Discount = 0.1 },
                    new Product { ProductName = "Nước Ép Cam", CategoryID = categories[0].CategoryID, Price = 40000, AvailableQuantity = 110, Discount = 0.08 },
                    new Product { ProductName = "Pizza Chay", CategoryID = categories[3].CategoryID, Price = 60000, AvailableQuantity = 70, Discount = 0.12 }
                };

                context.Products.AddRange(products);
                context.SaveChanges();


            }

            if (!context.Invoices.Any())
            {
                var customers = context.Customers.ToList();
                var employees = context.Employees.Where(e => e.RoleID != 2).ToList(); // Loại bỏ quản lý
                var random = new Random();

                var invoices = new List<Invoice>();
                for (int i = 0; i < 10; i++)
                {
                    invoices.Add(new Invoice
                    {
                        EmployeeID = employees[random.Next(employees.Count)].EmployeeID,
                        CustomerID = customers[random.Next(customers.Count)].CustomerID,
                        CreateAt = DateTime.Now.AddDays(-random.Next(1, 30)),
                        Discount = random.Next(5, 20) / 100.0,
                        PaymentMethod = (PaymentMethods)random.Next(0, 3),
                        Status = (InvoiceStatus)random.Next(0, 3),
                        Total = random.Next(50000, 500000),
                        DeliveryAddress = "Địa chỉ giao hàng " + (i + 1),
                        DeliveryStatus = (DeliveryStatus)random.Next(0, 2),
                        Feedback = "Phản hồi đơn hàng " + (i + 1),
                        Star = random.Next(1, 6)
                    });
                }
                context.Invoices.AddRange(invoices);
                context.SaveChanges();
            }
            if (!context.InvoiceDetails.Any())
            {
                var invoices = context.Invoices.ToList();
                var products = context.Products.ToList();
                var random = new Random();

                var invoiceDetails = new List<InvoiceDetail>();
                for (int i = 0; i < 20; i++)
                {
                    var product = products[random.Next(products.Count)];
                    int quantity = random.Next(1, 5);
                    invoiceDetails.Add(new InvoiceDetail
                    {
                        InvoiceID = invoices[random.Next(invoices.Count)].InvoiceID,
                        ProductID = product.ProductID,
                        Quantity = quantity,
                        Total = quantity * product.Price * (1 - product.Discount / 100)
                    });
                }

                context.InvoiceDetails.AddRange(invoiceDetails);
                context.SaveChanges();
            }
            if (!context.GiftPromotions.Any())
            {
                var customers = context.Customers.ToList();
                var products = context.Products.ToList();
                var random = new Random();

                var giftPromotions = new List<GiftPromotion>();
                for (int i = 0; i < 5; i++)
                {
                    var customer = customers[random.Next(customers.Count)];
                    var product = products[random.Next(products.Count)];
                    var startDate = DateTime.Now.AddDays(-random.Next(1, 30));
                    var endDate = startDate.AddDays(random.Next(5, 20));

                    giftPromotions.Add(new GiftPromotion
                    {
                        CustomerID = customer.CustomerID,
                        ProductID = product.ProductID,
                        GiftPromotionName = $"Khuyến mãi {i + 1}",
                        Quantity = random.Next(1, 5),
                        Status = GiftPromotionStatus.Active,
                        StartDate = startDate,
                        EndDate = endDate
                    });
                }

                context.GiftPromotions.AddRange(giftPromotions);
                context.SaveChanges();
            }
        }
    }
}