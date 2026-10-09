import 'package:weaver/core/network/api_endpoint.dart';

class FixedApiEndpoint implements ApiEndpoint {
  const FixedApiEndpoint(this.baseUrl);

  @override
  final String baseUrl;
}
