Hướng Dẫn Sử Dụng API với Postman
Tài liệu này hướng dẫn cách sử dụng các endpoint của Customer API thông qua Postman. Đường dẫn cơ sở cho tất cả các yêu cầu là http://localhost:5192.
Yêu Cầu Cần Thiết

Postman: Cài đặt Postman để thử nghiệm các yêu cầu API.
API Đang Chạy: Đảm bảo API đang chạy trên http://localhost:5192.
Xác Thực: Một số endpoint yêu cầu token JWT với vai trò "Customer". Sử dụng endpoint /login để lấy token.

Thiết Lập Postman

Tạo Bộ Sưu Tập Mới:
Mở Postman và tạo một bộ sưu tập mới có tên "Customer API".


Biến Môi Trường:
Tạo một môi trường và thêm biến:
base_url: http://localhost:5192
access_token: Lưu JWT token sau khi đăng nhập.
refresh_token: Lưu refresh token.





Các Endpoint API
1. Đăng Ký Tài Khoản
URL: POST {{base_url}}/api/customers/registerContent-Type: multipart/form-dataXác Thực: Không yêu cầu (AllowAnonymous)
Body (form-data):



Key
Type
Required
Example



CustomerName
Text
Yes
Nguyễn Văn A


Address
Text
Yes
123 Đường Láng, Hà Nội


PhoneNumber
Text
Yes
0123456789


Password
Text
Yes
MatKhau123


AvtFile
File
No
(chọn file ảnh)


Ví Dụ Trong Postman:

Chọn POST, nhập URL.
Vào tab Body, chọn form-data, thêm các key như trên.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Customer registered successfully"


2. Đăng Nhập
URL: POST {{base_url}}/api/customers/loginContent-Type: application/jsonXác Thực: Không yêu cầu (AllowAnonymous)
Body (JSON):



Key
Type
Required
Example



PhoneNumber
String
Yes
0123456789


Password
String
Yes
MatKhau123


Ví Dụ Trong Postman:

Chọn POST, nhập URL.
Vào tab Body, chọn raw và JSON, nhập:{
  "PhoneNumber": "0123456789",
  "Password": "MatKhau123"
}


Trong tab Tests, thêm:const response = pm.response.json();
pm.environment.set("access_token", response.token);
pm.environment.set("refresh_token", response.refreshToken);


Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body:{
  "token": "<access_token>",
  "refreshToken": "<refresh_token>"
}




3. Lấy Thông Tin Hồ Sơ
URL: GET {{base_url}}/api/customers/profileContent-Type: Không yêu cầuXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Ví Dụ Trong Postman:

Chọn GET, nhập URL.
Vào tab Headers, thêm Authorization: Bearer {{access_token}}.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body:{
  "CustomerName": "Nguyễn Văn A",
  "Address": "123 Đường Láng, Hà Nội",
  "PhoneNumber": "0123456789",
  "AvatarUrl": "<url_ảnh>"
}




4. Đổi Mật Khẩu
URL: PUT {{base_url}}/api/customers/passwordContent-Type: application/jsonXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Body (JSON):



Key
Type
Required
Example



OldPassword
String
Yes
MatKhau123


NewPassword
String
Yes
MatKhauMoi123


Ví Dụ Trong Postman:

Chọn PUT, nhập URL.
Thêm header Authorization.
Vào tab Body, chọn raw và JSON, nhập:{
  "OldPassword": "MatKhau123",
  "NewPassword": "MatKhauMoi123"
}


Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Password changed successfully"


5. Cập Nhật Hồ Sơ
URL: PUT {{base_url}}/api/customers/profileContent-Type: application/jsonXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Body (JSON):



Key
Type
Required
Example



CustomerName
String
Yes
Trần Thị B


Address
String
Yes
456 Đường Láng, Hà Nội


PhoneNumber
String
Yes
0987654321


Ví Dụ Trong Postman:

Chọn PUT, nhập URL.
Thêm header Authorization.
Vào tab Body, chọn raw và JSON, nhập:{
  "CustomerName": "Trần Thị B",
  "Address": "456 Đường Láng, Hà Nội",
  "PhoneNumber": "0987654321"
}


Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Profile updated successfully"


6. Cập Nhật Ảnh Đại Diện
URL: PUT {{base_url}}/api/customers/profile/avtContent-Type: multipart/form-dataXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Body (form-data):



Key
Type
Required
Example



avtFile
File
Yes
(chọn file ảnh)


Ví Dụ Trong Postman:

Chọn PUT, nhập URL.
Thêm header Authorization.
Vào tab Body, chọn form-data, thêm key avtFile với tệp ảnh.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Profile updated successfully"


7. Xóa Tài Khoản
URL: DELETE {{base_url}}/api/customers/account/statusContent-Type: Không yêu cầuXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Ví Dụ Trong Postman:

Chọn DELETE, nhập URL.
Thêm header Authorization.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Account deleted successfully"


8. Kích Hoạt Tài Khoản
URL: PUT {{base_url}}/api/customers/account/statusContent-Type: Không yêu cầuXác Thực: Bearer Token (Vai trò: Customer)
Headers:



Key
Value



Authorization
Bearer {{access_token}}


Ví Dụ Trong Postman:

Chọn PUT, nhập URL.
Thêm header Authorization.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Account status updated successfully"


9. Làm Mới Token
URL: POST {{base_url}}/api/customers/newTokenContent-Type: application/jsonXác Thực: Không yêu cầu (AllowAnonymous)
Body (JSON):



Key
Type
Required
Example



RefreshToken
String
Yes
{{refresh_token}}


Ví Dụ Trong Postman:

Chọn POST, nhập URL.
Vào tab Body, chọn raw và JSON, nhập:{
  "RefreshToken": "{{refresh_token}}"
}


Trong tab Tests, thêm:const response = pm.response.json();
pm.environment.set("access_token", response.token);
pm.environment.set("refresh_token", response.refreshToken);


Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body:{
  "token": "<new_access_token>",
  "refreshToken": "<new_refresh_token>"
}




10. Đăng Xuất
URL: POST {{base_url}}/api/customers/logoutContent-Type: application/jsonXác Thực: Không yêu cầu (AllowAnonymous)
Body (JSON):



Key
Type
Required
Example



(none)
String
Yes
{{refresh_token}}


Ví Dụ Trong Postman:

Chọn POST, nhập URL.
Vào tab Body, chọn raw và JSON, nhập chuỗi {{refresh_token}}.
Gửi yêu cầu.

Phản Hồi Mong Đợi:

Trạng thái: 200 OK
Body: "Logged out successfully"

Lưu Ý

Xử Lý Lỗi:
400 Bad Request: Dữ liệu đầu vào không hợp lệ hoặc thiếu trường bắt buộc.
401 Unauthorized: Token không hợp lệ, thiếu token hoặc quyền không đủ.
404 Not Found: Không tìm thấy tài nguyên (ví dụ: khách hàng).


Quản Lý Token:
Lưu access_token và refresh_token an toàn trong biến môi trường.
Sử dụng refresh token để lấy access token mới khi token hiện tại hết hạn.


Trình Tự Kiểm Tra:
Bắt đầu với register hoặc login để lấy token.
Sử dụng access_token cho các endpoint yêu cầu xác thực.
Làm mới token khi cần bằng endpoint newToken.
Kiểm tra logout để hủy refresh token.



Hướng dẫn này cung cấp cái nhìn tổng quan về cách tương tác với Customer API bằng Postman. Để biết thêm chi tiết, vui lòng tham khảo mã nguồn API hoặc liên hệ với nhóm phát triển.
