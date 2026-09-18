import 'package:weaver/features/auth/models/auth_session.dart';

abstract class AuthRepository {
  Future<AuthSession> register(String username, String password);

  Future<AuthSession> login(String username, String password);

  Future<AuthSession> refresh(String refreshToken);

  Future<void> logout(String refreshToken);
}
