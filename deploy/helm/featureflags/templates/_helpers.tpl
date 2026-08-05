{{/* Naming. */}}

{{- define "featureflags.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "featureflags.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name (include "featureflags.name" .) | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{- define "featureflags.labels" -}}
helm.sh/chart: {{ printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
app.kubernetes.io/name: {{ include "featureflags.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "featureflags.selectorLabels" -}}
app.kubernetes.io/name: {{ include "featureflags.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}

{{/*
The host portion of `origin`. The ingress needs a bare hostname while the auth service needs the
whole origin, so both come from the one value rather than being configured twice and drifting.

Bare meaning no scheme, no port, and no path. An Ingress `host` is a DNS name and nothing else —
`flags.example.com:8443` is rejected — while the origin the browser sends, and therefore the one
Better Auth has to trust, does include the port. Those two differ, so this derives one from the
other rather than asking for both and letting them disagree.
*/}}
{{- define "featureflags.host" -}}
{{- $authority := include "featureflags.origin" . | trimPrefix "https://" | trimPrefix "http://" -}}
{{- $authority = (splitList "/" $authority) | first -}}
{{- (splitList ":" $authority) | first -}}
{{- end -}}

{{/*
`origin` with any trailing slash removed.

An origin is a scheme, a host, and a port — never a trailing slash. A browser sends
`https://flags.example.com`, so `https://flags.example.com/` in the trusted-origins list matches
nothing and sign-in fails with an error that says only that the origin is invalid. Normalising
here means a value that reads correctly to a person also behaves correctly.
*/}}
{{- define "featureflags.origin" -}}
{{- required "origin is required, e.g. https://flags.example.com" .Values.origin | trimSuffix "/" -}}
{{- end -}}

{{- define "featureflags.serverImage" -}}
{{- printf "%s/%s/featureflags-server:%s" .Values.image.registry .Values.image.repository (.Values.image.tag | default .Chart.AppVersion) -}}
{{- end -}}

{{- define "featureflags.authImage" -}}
{{- printf "%s/%s/featureflags-auth:%s" .Values.image.registry .Values.image.repository (.Values.image.tag | default .Chart.AppVersion) -}}
{{- end -}}

{{- define "featureflags.secretName" -}}
{{- default (printf "%s-secrets" (include "featureflags.fullname" .)) .Values.betterAuth.existingSecret -}}
{{- end -}}

{{/* In-cluster addresses. Neither service is reachable from outside the namespace. */}}
{{- define "featureflags.authAddress" -}}
{{- printf "http://%s-auth:8080" (include "featureflags.fullname" .) -}}
{{- end -}}

{{- define "featureflags.redisUrl" -}}
{{- if .Values.redis.bundled -}}
{{- printf "redis://%s-redis:6379" (include "featureflags.fullname" .) -}}
{{- else -}}
{{- required "redis.external.url is required when redis.bundled is false" .Values.redis.external.url -}}
{{- end -}}
{{- end -}}

{{/*
Where the database URL comes from, as an env var fragment rather than a plain value — it carries
a password, so it is only ever read from a Secret.

Both the server and the auth service consume it. They share one database and separate by schema,
so this deliberately cannot be configured to two different places.
*/}}
{{- define "featureflags.databaseUrlEnv" -}}
- name: FEATUREFLAGS_DATABASE_URL
  valueFrom:
    secretKeyRef:
      {{- if and (not .Values.postgres.bundled) .Values.postgres.external.existingSecret }}
      name: {{ .Values.postgres.external.existingSecret }}
      {{- else }}
      name: {{ include "featureflags.secretName" . }}
      {{- end }}
      key: FEATUREFLAGS_DATABASE_URL
{{- end -}}

{{- define "featureflags.betterAuthSecretEnv" -}}
- name: BETTER_AUTH_SECRET
  valueFrom:
    secretKeyRef:
      name: {{ include "featureflags.secretName" . }}
      key: BETTER_AUTH_SECRET
{{- end -}}
