import 'package:flutter/material.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/network/api_config.dart';

void main() {
  ApiConfig.validate();
  configureDependencies();
  runApp(const WeaverApp());
}
