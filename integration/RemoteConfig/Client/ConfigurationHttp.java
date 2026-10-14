package com.gindemit.configuration;

import android.util.Base64;
import com.unity3d.player.UnityPlayer;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URI;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Semaphore;

/** Explicitly owned config-only adapter. No identity, credentials, TLS override or singleton. */
public final class ConfigurationHttp {
    private final URI normal;
    private final URI control;
    private final boolean loopback;
    private final ExecutorService executor = Executors.newFixedThreadPool(3);
    private final Semaphore normalSlots = new Semaphore(2);
    private final Semaphore controlSlots = new Semaphore(1);
    private final Map<String, Request> pending = new HashMap<>();
    private boolean disposed;

    public ConfigurationHttp(String normalRoot, String controlRoot, boolean allowLoopback) {
        if (java.net.CookieHandler.getDefault() != null) throw new IllegalStateException("ambient_cookies_forbidden");
        loopback = allowLoopback;
        normal = root(normalRoot);
        control = root(controlRoot);
    }
    private URI root(String text) {
        URI value = URI.create(text);
        if (text.length() > 4096 || value.getUserInfo() != null || value.getQuery() != null || value.getFragment() != null || value.getHost() == null || !value.getPath().endsWith("/") || value.getPath().contains("%") || value.getPath().contains("..") || !transportAllowed(value)) throw new IllegalArgumentException("origin_invalid");
        return value;
    }
    private boolean transportAllowed(URI value) {
        return "https".equals(value.getScheme()) || (loopback && "http".equals(value.getScheme()) && ("localhost".equals(value.getHost()) || "127.0.0.1".equals(value.getHost()) || "[::1]".equals(value.getHost()) || "::1".equals(value.getHost())));
    }
    private boolean matches(URI address, URI root) {
        return address.getScheme().equals(root.getScheme()) && address.getHost().equals(root.getHost()) && address.getPort() == root.getPort() && address.getPath().startsWith(root.getPath());
    }
    private boolean immutable(String path) {
        return path.matches("(assets|selectors|manifests)/[0-9a-f]{64}\\.json");
    }
    private Semaphore slot(URI address) {
        if (address.getUserInfo() != null || address.getQuery() != null || address.getFragment() != null || !transportAllowed(address)) throw new IllegalArgumentException("url_invalid");
        if (matches(address, normal)) { String path = address.getPath().substring(normal.getPath().length()); if (path.equals("current.json") || immutable(path)) return normalSlots; }
        if (matches(address, control) && address.getPath().substring(control.getPath().length()).equals("control.json")) return controlSlots;
        throw new IllegalArgumentException("url_scope");
    }
    private static boolean id(String value) { return value != null && value.matches("[0-9a-f]{16}"); }
    private static String encoded(String value) { return Base64.encodeToString(value.getBytes(StandardCharsets.UTF_8), Base64.NO_WRAP); }
    private static void failure(String id, String target, int code) { UnityPlayer.UnitySendMessage(target, "OnConfigurationHttp", id + "|" + code + "||||"); }
    public synchronized void get(String address, String id, String target, int maximum, String etag, boolean immutable) {
        if (!id(id) || target == null || target.isEmpty() || target.length() > 128) return;
        if (disposed || pending.containsKey(id) || address == null || address.length() > 4096 || maximum < 1 || maximum > 1048576 || etag == null || etag.length() > 4096 || etag.contains("\r") || etag.contains("\n")) { failure(id, target, -1); return; }
        Semaphore slots;
        try { slots = slot(URI.create(address)); } catch (RuntimeException invalid) { failure(id, target, -1); return; }
        if (!slots.tryAcquire()) { failure(id, target, -2); return; }
        Request request = new Request(address, id, target, maximum, etag, slots);
        pending.put(id, request);
        try { executor.execute(request); } catch (RuntimeException unavailable) { pending.remove(id); slots.release(); failure(id, target, -1); }
    }
    private final class Request implements Runnable {
        final String address;
        final String id;
        final String target;
        final int maximum;
        final String etag;
        final Semaphore slots;
        volatile boolean cancelled;
        volatile HttpURLConnection connection;
        Request(String address, String id, String target, int maximum, String etag, Semaphore slots) { this.address = address; this.id = id; this.target = target; this.maximum = maximum; this.etag = etag; this.slots = slots; }
        void send(int status, String mime, String validator, String retry, byte[] body) {
            synchronized (ConfigurationHttp.this) {
                if (cancelled || pending.get(id) != this) return;
                UnityPlayer.UnitySendMessage(target, "OnConfigurationHttp", id + "|" + status + "|" + encoded(validator) + "|" + retry + "|" + encoded(mime) + "|" + Base64.encodeToString(body, Base64.NO_WRAP));
            }
        }
        @Override public void run() {
            try {
                if (cancelled) return;
                connection = (HttpURLConnection) new URL(address).openConnection();
                connection.setConnectTimeout(5000);
                connection.setReadTimeout(5000);
                connection.setInstanceFollowRedirects(false);
                connection.setUseCaches(true);
                connection.setRequestMethod("GET");
                connection.setAllowUserInteraction(false);
                connection.setRequestProperty("Cookie", "");
                if (!etag.isEmpty()) connection.setRequestProperty("If-None-Match", etag);
                // Android's host stack negotiates/decodes compression; no custom decoder.
                int status = connection.getResponseCode();
                String mime = connection.getContentType(); mime = mime == null ? "" : mime.split(";", 2)[0].trim().toLowerCase(Locale.ROOT);
                String validator = connection.getHeaderField("ETag"); validator = validator == null ? "" : validator;
                if (validator.length() > 4096 || validator.contains("\r") || validator.contains("\n")) throw new IOException("validator_invalid");
                String retry = connection.getHeaderField("Retry-After"); retry = retry != null && retry.matches("[0-9]{1,6}") ? Integer.toString(Math.min(900, Integer.parseInt(retry))) : "";
                if (status != 200) { send(status, mime, validator, retry, new byte[0]); return; }
                if (!mime.equals("application/json")) throw new IOException("mime_invalid");
                try (InputStream input = connection.getInputStream(); ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                    byte[] buffer = new byte[8192]; int total = 0;
                    while (true) { if (cancelled) return; int count = input.read(buffer); if (count == -1) break; total += count; if (total > maximum) { send(-4, "", "", "", new byte[0]); return; } output.write(buffer, 0, count); }
                    send(status, mime, validator, retry, output.toByteArray());
                }
            } catch (java.net.SocketTimeoutException timeout) { if (!cancelled) failure(id, target, -5); }
            catch (Exception unavailable) { if (!cancelled) failure(id, target, -1); }
            finally { if (connection != null) connection.disconnect(); synchronized (ConfigurationHttp.this) { if (pending.get(id) == this) pending.remove(id); } slots.release(); }
        }
        void cancel() { cancelled = true; HttpURLConnection active = connection; if (active != null) active.disconnect(); }
    }
    public synchronized void cancel(String id) { Request request = pending.remove(id); if (request != null) request.cancel(); }
    public synchronized void dispose() { if (disposed) return; disposed = true; for (Request request : pending.values()) request.cancel(); pending.clear(); executor.shutdownNow(); }
}
