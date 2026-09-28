import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/network_exception.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';

void main() {
  for (final status in [400, 403, 404, 409]) {
    test('shows the server message for a $status', () {
      final error = ApiException('Name is taken (409).', statusCode: status);

      expect(adminErrorMessage(error, 'fallback'), 'Name is taken (409).');
    });
  }

  test('falls back for a server error', () {
    const error = ApiException('boom', statusCode: 500);

    expect(adminErrorMessage(error, 'fallback'), 'fallback');
  });

  test('falls back when the server is unreachable', () {
    expect(adminErrorMessage(const NetworkException(), 'fallback'), 'fallback');
  });
}
