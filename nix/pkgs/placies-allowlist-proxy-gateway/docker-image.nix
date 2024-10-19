{
  dockerTools,
  placies-allowlist-proxy-gateway,
  busybox,
  cacert
}:
dockerTools.buildLayeredImage {
  name = "placies-allowlist-proxy-gateway-docker-image";
  contents = [
    busybox
    cacert
    placies-allowlist-proxy-gateway
  ];
  config = {
    Env = [
      "SSL_CERT_FILE=${cacert}/etc/ssl/certs/ca-bundle.crt"
    ];
    Cmd = [ "/bin/Placies.Gateways.AllowlistProxyGateway.Server" ];
  };
}
