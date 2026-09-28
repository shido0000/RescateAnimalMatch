"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const LINKS: Array<{ href: string; label: string }> = [
  { href: "/dashboard", label: "📊 Dashboard" },
  { href: "/shelters", label: "🏠 Refugios" },
  { href: "/donations", label: "💚 Donaciones" },
  { href: "/events", label: "📅 Eventos" },
  { href: "/content", label: "🧩 Contenido" },
  { href: "/login", label: "🔐 Login" },
];

export function NavLinks() {
  const pathname = usePathname();
  return (
    <nav>
      {LINKS.map((l) => (
        <Link
          key={l.href}
          href={l.href}
          className={pathname === l.href ? "active" : undefined}
        >
          {l.label}
        </Link>
      ))}
    </nav>
  );
}
