import 'package:weaver/core/network/server_origin_store.dart';
import 'package:weaver/core/network/server_settings.dart';

/// flutter_secure_storage has no test-environment implementation.
class InMemoryServerOriginStore implements ServerOriginStore {
  InMemoryServerOriginStore([this.origin]);

  String? origin;

  @override
  Future<String?> readOrigin() async => origin;

  @override
  Future<void> writeOrigin(String value) async => origin = value;
}

/// Settings for a web build, where the server is fixed and sign-in doesn't
/// ask for one.
ServerSettings webServerSettings() => ServerSettings(
  InMemoryServerOriginStore(),
  buildTimeOrigin: '',
  isWeb: true,
);
