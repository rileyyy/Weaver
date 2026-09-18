import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_config.dart';

@module
abstract class NetworkModule {
  @lazySingleton
  http.Client get httpClient => http.Client();

  @Named('apiBaseUrl')
  String get apiBaseUrl => ApiConfig.baseUrl;
}
