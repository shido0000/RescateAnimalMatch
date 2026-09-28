"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

/**
 * /login — Firebase Auth email/password restricted to admin role.
 * Flow: signIn -> fetch ID token -> GET /api/auth/session (verifies the
 * users/{uid}.role is admin/editor) -> redirect to /dashboard.
 */
export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function getFirebaseToken(): Promise<string> {
    // Lazy import so the page builds without configured credentials.
    const { firebaseAuth } = await import("@/lib/firebase");
    const { signInWithEmailAndPassword } = await import("firebase/auth");
    const cred = await signInWithEmailAndPassword(
      firebaseAuth(),
      email,
      password,
    );
    return cred.user.getIdToken();
  }

  /** Dev fallback when Firebase env vars are absent (local/demo login). */
  function devToken(): string {
    if (email.endsWith("@localhost") && password === "admin123") {
      return "dev-admin-token";
    }
    throw new Error("credenciales_invalidas");
  }

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const token = process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID
        ? await getFirebaseToken()
        : devToken();
      const res = await fetch("/api/auth/session", {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.error ?? "no_autorizado");
      }
      sessionStorage.setItem("ram_admin_token", token);
      router.push("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "error_desconocido");
    } finally {
      setBusy(false);
    }
  }

  return (
    <form className="auth" onSubmit={onSubmit}>
      <h1>Iniciar sesión</h1>
      <p className="muted">Acceso restringido a administradores.</p>
      <input
        type="email"
        placeholder="correo@refugio.org"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        required
      />
      <input
        type="password"
        placeholder="Contraseña"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        required
      />
      {error && <span className="error">{error}</span>}
      <button type="submit" disabled={busy}>
        {busy ? "Verificando…" : "Entrar"}
      </button>
    </form>
  );
}
