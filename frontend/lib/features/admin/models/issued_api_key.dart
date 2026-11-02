import 'package:weaver/features/admin/models/api_key.dart';

/// A newly issued key with its plaintext [key], which the server never
/// returns again.
class IssuedApiKey {
  const IssuedApiKey({required this.apiKey, required this.key});

  factory IssuedApiKey.fromJson(Map<String, dynamic> json) => IssuedApiKey(
    apiKey: ApiKey.fromJson(json['apiKey'] as Map<String, dynamic>),
    key: json['key'] as String,
  );

  final ApiKey apiKey;
  final String key;
}
