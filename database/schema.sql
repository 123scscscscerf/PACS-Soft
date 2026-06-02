CREATE TABLE IF NOT EXISTS Persons (
  uid BIGINT AUTO_INCREMENT PRIMARY KEY,
  card_id VARCHAR(64) UNIQUE,
  name VARCHAR(100) NOT NULL,
  surname VARCHAR(100) NOT NULL,
  patronymic VARCHAR(100),
  birthdate DATE,
  type ENUM('student','staff','admin','special') NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Students (
  uid BIGINT PRIMARY KEY,
  course INT NOT NULL,
  allowed_in TINYINT(1) NOT NULL DEFAULT 1,
  is_blocked TINYINT(1) NOT NULL DEFAULT 0,
  is_card_stolen TINYINT(1) NOT NULL DEFAULT 0,
  CONSTRAINT fk_students_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Personnel (
  uid BIGINT PRIMARY KEY,
  position VARCHAR(150) NOT NULL,
  department VARCHAR(150) NOT NULL,
  CONSTRAINT fk_personnel_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Admins (
  uid BIGINT PRIMARY KEY,
  login VARCHAR(100) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  role ENUM('superadmin','security','teacher') NOT NULL,
  privileges_json JSON,
  CONSTRAINT fk_admins_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Readers (
  reader_id VARCHAR(64) PRIMARY KEY,
  location VARCHAR(150) NOT NULL,
  description VARCHAR(255),
  status VARCHAR(50) NOT NULL DEFAULT 'online'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Access_Events (
  event_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  uid BIGINT NULL,
  card_id VARCHAR(64) NOT NULL,
  reader_id VARCHAR(64) NOT NULL,
  event_time DATETIME NOT NULL,
  event_type ENUM('IN','OUT','DENY') NOT NULL,
  result ENUM('granted','denied') NOT NULL,
  reason VARCHAR(255),
  event_hash CHAR(64) NOT NULL UNIQUE,
  INDEX ix_access_events_time (event_time),
  INDEX ix_access_events_card (card_id),
  INDEX ix_access_events_uid (uid),
  CONSTRAINT fk_access_events_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Failed_Attempts (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  card_id VARCHAR(64) NOT NULL,
  reader_id VARCHAR(64) NOT NULL,
  attempt_time DATETIME NOT NULL,
  reason VARCHAR(255),
  INDEX ix_failed_attempts_time (attempt_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Person_Status (
  uid BIGINT PRIMARY KEY,
  last_reader_id VARCHAR(64),
  last_event_time DATETIME,
  state ENUM('inside','outside') NOT NULL DEFAULT 'outside',
  CONSTRAINT fk_status_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Audit_Log (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  uid BIGINT NULL,
  action VARCHAR(100) NOT NULL,
  old_value TEXT,
  new_value TEXT,
  changed_by BIGINT NULL,
  changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_audit_changed_at (changed_at),
  CONSTRAINT fk_audit_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Security_Events (
  event_id BIGINT AUTO_INCREMENT PRIMARY KEY,
  uid BIGINT NULL,
  card_id VARCHAR(64),
  reader_id VARCHAR(64),
  event_time DATETIME NOT NULL,
  event_type ENUM('fire','tamper','forced_entry') NOT NULL,
  system_state VARCHAR(50) NOT NULL,
  dahua_alert_state VARCHAR(100),
  INDEX ix_security_events_time (event_time),
  CONSTRAINT fk_security_events_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Access_Rules (
  id BIGINT AUTO_INCREMENT PRIMARY KEY,
  uid BIGINT NOT NULL,
  reader_id VARCHAR(64) NOT NULL,
  allowed TINYINT(1) NOT NULL DEFAULT 1,
  valid_from TIME NULL,
  valid_to TIME NULL,
  CONSTRAINT uq_access_rules UNIQUE(uid, reader_id),
  CONSTRAINT fk_rules_persons FOREIGN KEY (uid) REFERENCES Persons(uid) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
