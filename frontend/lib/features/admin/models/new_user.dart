/// What the create-user dialog collects.
class NewUser {
  const NewUser({
    required this.username,
    required this.password,
    required this.isAdmin,
  });

  final String username;
  final String password;
  final bool isAdmin;

  Map<String, dynamic> toJson() => {
    'username': username,
    'password': password,
    'isAdmin': isAdmin,
  };
}
