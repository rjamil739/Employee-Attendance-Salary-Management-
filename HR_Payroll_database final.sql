-- PostgreSQL 15+ required.
-- HR / Attendance / Payroll / Loans / Finance / Benefits / Workflow / Audit database.
-- Ordered intentionally: ALL TABLES FIRST, then helpers/indexes/triggers/views.

-- ============================================================================
-- TRANSACTION
-- ============================================================================

-- PostgreSQL 15+ required (uses UNIQUE NULLS NOT DISTINCT and GiST exclusion constraints).
-- Complete fixed HR, attendance, payroll, loans, finance, benefits, workflow and audit schema.



BEGIN;

-- ============================================================================
-- REQUIRED EXTENSIONS
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE EXTENSION IF NOT EXISTS btree_gist;

-- ============================================================================
-- SCHEMAS
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS core;

CREATE SCHEMA IF NOT EXISTS auth;

CREATE SCHEMA IF NOT EXISTS hr;

CREATE SCHEMA IF NOT EXISTS attendance;

CREATE SCHEMA IF NOT EXISTS leave_mgmt;

CREATE SCHEMA IF NOT EXISTS payroll;

CREATE SCHEMA IF NOT EXISTS loans;

CREATE SCHEMA IF NOT EXISTS finance;

CREATE SCHEMA IF NOT EXISTS benefits;

CREATE SCHEMA IF NOT EXISTS workflow;

CREATE SCHEMA IF NOT EXISTS audit;

CREATE SCHEMA IF NOT EXISTS reporting;

-- ============================================================================
-- 1. ALL TABLES
-- ============================================================================

-- Audit rule: every table except core.company_profile contains created_by and updated_by user references.
-- System-generated rows may leave these audit user columns NULL.

CREATE TABLE core.company_profile (
    id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    company_name text NOT NULL,
    logo bytea,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE auth.user_account (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name varchar(100) NOT NULL UNIQUE,
    email varchar(320),
    password_hash text,
    phone_number text,

    lockout_enabled boolean NOT NULL DEFAULT true,
    access_failed_count integer NOT NULL DEFAULT 0
        CHECK (access_failed_count >= 0),

    display_name text NOT NULL,

    has_all_branch_access boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,

    last_login_at timestamptz,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE core.branch (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL UNIQUE,
    name text NOT NULL,
    logo bytea,
    is_head_office boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE core.department (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL,
    parent_department_id uuid,
    code varchar(50) NOT NULL,
    name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (branch_id, code),
    UNIQUE (branch_id, id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (branch_id, parent_department_id)
        REFERENCES core.department(branch_id, id),
    CHECK (parent_department_id IS NULL OR parent_department_id <> id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);



CREATE TABLE core.designation (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL,
    department_id uuid NOT NULL,
    code varchar(50) NOT NULL,
    name text NOT NULL,
    grade text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (department_id, code),
    UNIQUE (branch_id, department_id, id),
    FOREIGN KEY (branch_id, department_id)
        REFERENCES core.department(branch_id, id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE core.contractor (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL,
    name text NOT NULL,
    contact_person text,
    phone_number varchar(30),
    email varchar(320),
    address text,
    city varchar(100),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE core.contractor_branch (
    contractor_id uuid NOT NULL REFERENCES core.contractor(id) ON DELETE CASCADE,
    branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
    is_active boolean NOT NULL DEFAULT true,
    PRIMARY KEY (contractor_id, branch_id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE auth.role (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(50) NOT NULL UNIQUE,
    name varchar(100) NOT NULL,
    description text,
    scope_type text NOT NULL DEFAULT 'branch'
        CHECK (scope_type IN ('company', 'branch')),
    is_system boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE auth.user_role (
    user_id uuid NOT NULL,
    role_id uuid NOT NULL,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id)
        REFERENCES auth.user_account(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id)
        REFERENCES auth.role(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE auth.user_branch (
    user_id uuid NOT NULL REFERENCES auth.user_account(id) ON DELETE CASCADE,
    branch_id uuid NOT NULL REFERENCES core.branch(id) ON DELETE CASCADE,
    is_default boolean NOT NULL DEFAULT false,
    PRIMARY KEY (user_id, branch_id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE hr.employee (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_account_id uuid,
    employee_no varchar(50) NOT NULL,
    payroll_no varchar(50),
    first_name text NOT NULL,
    last_name text,
    father_name text,
    photo bytea,
    national_id_encrypted bytea,
    national_id_hash char(64),
    date_of_birth date,
    gender text CHECK (gender IS NULL OR gender IN ('male', 'female', 'other')),
    email varchar(320),
    mobile_phone text,
    address text,
    city varchar(100),
    district varchar(100),
    province varchar(100),
    postal_code varchar(20),
    hire_date date NOT NULL,
    confirmation_date date,
    termination_date date,
    employment_status text NOT NULL DEFAULT 'active'
        CHECK (employment_status IN ('draft', 'active', 'suspended', 'terminated', 'retired')),
    employment_type text NOT NULL DEFAULT 'permanent'
        CHECK (employment_type IN ('permanent', 'contract', 'temporary', 'daily_wage', 'intern')),
    preferred_payment_channel text NOT NULL DEFAULT 'cash'
        CHECK (preferred_payment_channel IN ('cash', 'bank')),
    preferred_bank_payment_type text
        CHECK (preferred_bank_payment_type IS NULL OR preferred_bank_payment_type IN ('online_transfer', 'cheque')),
    photo_object_key text,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by uuid,
    updated_at timestamptz NOT NULL DEFAULT now(),
    updated_by uuid,
    UNIQUE (employee_no),
    UNIQUE (payroll_no),
    UNIQUE (user_account_id),
    FOREIGN KEY (user_account_id)
        REFERENCES auth.user_account(id),
    FOREIGN KEY (created_by)
        REFERENCES auth.user_account(id),
    FOREIGN KEY (updated_by)
        REFERENCES auth.user_account(id),
    CHECK (termination_date IS NULL OR termination_date >= hire_date),
    CHECK (
        (preferred_payment_channel = 'cash' AND preferred_bank_payment_type IS NULL)
        OR (preferred_payment_channel = 'bank' AND preferred_bank_payment_type IS NOT NULL)
    )
);


CREATE TABLE hr.employee_fingerprint (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    employee_id uuid NOT NULL
        REFERENCES hr.employee(id) ON DELETE CASCADE,

    finger_type varchar(30) NOT NULL
        CHECK (finger_type IN (
            'right_thumb',
            'right_index',
            'right_middle',
            'right_ring',
            'right_little',
            'left_thumb',
            'left_index',
            'left_middle',
            'left_ring',
            'left_little'
        )),

    fingerprint_template bytea NOT NULL,

    template_format varchar(100),

    fingerprint_image bytea,
    image_content_type varchar(100),

    is_active boolean NOT NULL DEFAULT true,

    created_by uuid
        REFERENCES auth.user_account(id)
        ON DELETE SET NULL,

    updated_by uuid
        REFERENCES auth.user_account(id)
        ON DELETE SET NULL,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),

    UNIQUE (employee_id, finger_type)
);

CREATE TABLE hr.employee_assignment (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    department_id uuid NOT NULL,
    designation_id uuid NOT NULL,
    manager_employee_id uuid,
    effective_from date NOT NULL,
    effective_to date,

    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),

    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),

    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),

    FOREIGN KEY (branch_id, department_id)
        REFERENCES core.department(branch_id, id),

    FOREIGN KEY (branch_id, department_id, designation_id)
        REFERENCES core.designation(branch_id, department_id, id),

    FOREIGN KEY (manager_employee_id)
        REFERENCES hr.employee(id),

    CHECK (effective_to IS NULL OR effective_to >= effective_from),

    EXCLUDE USING gist (
        employee_id WITH =,
        daterange(
            effective_from,
            COALESCE(effective_to + 1, 'infinity'::date),
            '[)'
        ) WITH &&
    )
);

CREATE TABLE hr.employee_compensation (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    base_salary numeric(18,2) NOT NULL CHECK (base_salary >= 0),
    pay_frequency text NOT NULL DEFAULT 'monthly'
        CHECK (pay_frequency IN ('monthly', 'daily', 'hourly')),
    effective_from date NOT NULL,
    effective_to date,
    reason text,
    approved_by uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (approved_by)
        REFERENCES auth.user_account(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_employee_compensation_period
    EXCLUDE USING gist (
        employee_id WITH =,
        daterange(effective_from, COALESCE(effective_to + 1, 'infinity'::date), '[)') WITH &&
    )
);


CREATE TABLE hr.employee_bank_account (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    bank_name text NOT NULL,
    account_title text NOT NULL,
    iban_encrypted bytea,
    account_number_encrypted bytea,
    is_primary boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE hr.employee_document (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    document_type text NOT NULL,
    object_key text NOT NULL,
    original_file_name text NOT NULL,
    content_type text NOT NULL,
    checksum_sha256 char(64) NOT NULL,
    expires_on date,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.holiday_calendar (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid,
    code varchar(100) NOT NULL,
    name text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE NULLS NOT DISTINCT (branch_id, code),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.holiday (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    calendar_id uuid NOT NULL,
    holiday_date date NOT NULL,
    name text NOT NULL,
    is_paid boolean NOT NULL DEFAULT true,
    UNIQUE (calendar_id, holiday_date),
    FOREIGN KEY (calendar_id)
        REFERENCES attendance.holiday_calendar(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.shift (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    start_local_time time NOT NULL,
    end_local_time time NOT NULL,
    break_minutes integer NOT NULL DEFAULT 0 CHECK (break_minutes >= 0),
    crosses_midnight boolean NOT NULL DEFAULT false,
    scheduled_minutes integer NOT NULL CHECK (scheduled_minutes > 0),
    is_flexible boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.attendance_policy (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    late_grace_minutes integer NOT NULL DEFAULT 0 CHECK (late_grace_minutes >= 0),
    early_out_grace_minutes integer NOT NULL DEFAULT 0 CHECK (early_out_grace_minutes >= 0),
    half_day_below_minutes integer CHECK (half_day_below_minutes > 0),
    absent_below_minutes integer CHECK (absent_below_minutes >= 0),
    overtime_after_minutes integer NOT NULL DEFAULT 0 CHECK (overtime_after_minutes >= 0),
    overtime_rounding_minutes integer NOT NULL DEFAULT 1 CHECK (overtime_rounding_minutes > 0),
    overtime_multiplier numeric(6,3) NOT NULL DEFAULT 1.000 CHECK (overtime_multiplier >= 0),
    holiday_overtime_multiplier numeric(6,3) NOT NULL DEFAULT 2.000 CHECK (holiday_overtime_multiplier >= 0),
    weekly_off_days smallint[] NOT NULL DEFAULT ARRAY[7]::smallint[],
    require_overtime_approval boolean NOT NULL DEFAULT true,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (code),
    CHECK (weekly_off_days <@ ARRAY[1,2,3,4,5,6,7]::smallint[]),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.employee_schedule (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    shift_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    holiday_calendar_id uuid,
    effective_from date NOT NULL,
    effective_to date,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (shift_id)
        REFERENCES attendance.shift(id),
    FOREIGN KEY (policy_id)
        REFERENCES attendance.attendance_policy(id),
    FOREIGN KEY (holiday_calendar_id)
        REFERENCES attendance.holiday_calendar(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_employee_schedule_period
    EXCLUDE USING gist (
        employee_id WITH =,
        daterange(effective_from, COALESCE(effective_to + 1, 'infinity'::date), '[)') WITH &&
    )
);

CREATE TABLE attendance.zkt_device (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL,
    code varchar(100) NOT NULL,
    name text NOT NULL,
    model_name varchar(100),
    serial_number varchar(100),
    ip_address inet NOT NULL,
    port integer NOT NULL DEFAULT 4370 CHECK (port BETWEEN 1 AND 65535),
    protocol text NOT NULL DEFAULT 'tcp'
        CHECK (protocol IN ('tcp', 'udp')),
    device_number integer NOT NULL DEFAULT 1 CHECK (device_number > 0),
    communication_key_encrypted bytea,
    timezone_name varchar(100) NOT NULL DEFAULT 'Asia/Karachi',
    sync_interval_minutes integer NOT NULL DEFAULT 5
        CHECK (sync_interval_minutes > 0),
    is_active boolean NOT NULL DEFAULT true,
    last_sync_at timestamptz,
    last_sync_status text
        CHECK (last_sync_status IS NULL OR last_sync_status IN ('success', 'failed', 'in_progress')),
    last_sync_error text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (branch_id, code),
    UNIQUE (branch_id, id),
    UNIQUE (branch_id, ip_address, port, device_number),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.zkt_employee_mapping (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    zkt_device_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    zkt_user_id varchar(100) NOT NULL,
    card_number varchar(100),
    is_active boolean NOT NULL DEFAULT true,
    last_synced_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (zkt_device_id, zkt_user_id),
    UNIQUE (zkt_device_id, employee_id),
    FOREIGN KEY (zkt_device_id)
        REFERENCES attendance.zkt_device(id) ON DELETE CASCADE,
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.zkt_punch (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    public_id uuid NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    branch_id uuid NOT NULL,
    employee_id uuid,
    zkt_device_id uuid NOT NULL,
    zkt_user_id varchar(100) NOT NULL,
    punch_time timestamptz NOT NULL,
    punch_state smallint,
    verify_mode smallint,
    work_code varchar(100),
    direction text NOT NULL DEFAULT 'unknown'
        CHECK (direction IN ('in', 'out', 'break_out', 'break_in', 'unknown')),
    received_at timestamptz NOT NULL DEFAULT now(),
    raw_data text,
    UNIQUE NULLS NOT DISTINCT (zkt_device_id, zkt_user_id, punch_time, punch_state, verify_mode),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (branch_id, zkt_device_id)
        REFERENCES attendance.zkt_device(branch_id, id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.attendance_day (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    department_id uuid NOT NULL,
    work_date date NOT NULL,
    shift_id uuid,
    policy_id uuid,
    first_in_at timestamptz,
    last_out_at timestamptz,
    scheduled_minutes integer NOT NULL DEFAULT 0 CHECK (scheduled_minutes >= 0),
    worked_minutes integer NOT NULL DEFAULT 0 CHECK (worked_minutes >= 0),
    late_minutes integer NOT NULL DEFAULT 0 CHECK (late_minutes >= 0),
    early_out_minutes integer NOT NULL DEFAULT 0 CHECK (early_out_minutes >= 0),
    overtime_minutes integer NOT NULL DEFAULT 0 CHECK (overtime_minutes >= 0),
    break_minutes integer NOT NULL DEFAULT 0 CHECK (break_minutes >= 0),
    status text NOT NULL
        CHECK (status IN ('present', 'absent', 'late', 'half_day', 'leave', 'holiday', 'weekly_off', 'missing_punch')),
    entry_source text NOT NULL DEFAULT 'system'
        CHECK (entry_source IN ('manual', 'zkt', 'system')),
    manual_entry_by uuid,
    manual_entry_reason text,
    calculation_version integer NOT NULL DEFAULT 1,
    calculated_at timestamptz NOT NULL DEFAULT now(),
    locked_at timestamptz,
    notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (employee_id, work_date),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (branch_id, department_id)
        REFERENCES core.department(branch_id, id),
    FOREIGN KEY (shift_id)
        REFERENCES attendance.shift(id),
    FOREIGN KEY (policy_id)
        REFERENCES attendance.attendance_policy(id),
    FOREIGN KEY (manual_entry_by)
        REFERENCES auth.user_account(id),
    CHECK (
        (
            entry_source = 'manual'
            AND manual_entry_by IS NOT NULL
            AND manual_entry_reason IS NOT NULL
            AND btrim(manual_entry_reason) <> ''
        )
        OR (entry_source <> 'manual' AND manual_entry_by IS NULL AND manual_entry_reason IS NULL)
    ),
    CHECK (last_out_at IS NULL OR first_in_at IS NULL OR last_out_at >= first_in_at),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.adjustment_request (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    work_date date NOT NULL,
    requested_first_in_at timestamptz,
    requested_last_out_at timestamptz,
    reason text NOT NULL,
    status text NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
    requested_by uuid NOT NULL,
    decided_by uuid,
    decided_at timestamptz,
    decision_notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (requested_by)
        REFERENCES auth.user_account(id),
    FOREIGN KEY (decided_by)
        REFERENCES auth.user_account(id),
    CHECK (requested_last_out_at IS NULL OR requested_first_in_at IS NULL OR requested_last_out_at >= requested_first_in_at),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE attendance.overtime_request (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    work_date date NOT NULL,
    requested_minutes integer NOT NULL CHECK (requested_minutes > 0),
    approved_minutes integer CHECK (approved_minutes >= 0),
    multiplier numeric(6,3) NOT NULL DEFAULT 1.000 CHECK (multiplier >= 0),
    reason text,
    status text NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
    requested_by uuid NOT NULL,
    decided_by uuid,
    decided_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (employee_id, work_date),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (requested_by)
        REFERENCES auth.user_account(id),
    FOREIGN KEY (decided_by)
        REFERENCES auth.user_account(id),
    CHECK (approved_minutes IS NULL OR approved_minutes <= requested_minutes),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.leave_type (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    is_paid boolean NOT NULL DEFAULT true,
    requires_attachment boolean NOT NULL DEFAULT false,
    allow_half_day boolean NOT NULL DEFAULT true,
    max_consecutive_days numeric(6,2),
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.leave_policy (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    leave_type_id uuid NOT NULL,
    name text NOT NULL,
    annual_entitlement numeric(8,2) NOT NULL DEFAULT 0 CHECK (annual_entitlement >= 0),
    accrual_frequency text NOT NULL DEFAULT 'annual'
        CHECK (accrual_frequency IN ('monthly', 'annual', 'none')),
    carry_forward_limit numeric(8,2) NOT NULL DEFAULT 0 CHECK (carry_forward_limit >= 0),
    allow_negative_balance boolean NOT NULL DEFAULT false,
    effective_from date NOT NULL,
    effective_to date,
    FOREIGN KEY (leave_type_id)
        REFERENCES leave_mgmt.leave_type(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.employee_leave_policy (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (policy_id)
        REFERENCES leave_mgmt.leave_policy(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.leave_request (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    requested_days numeric(8,2) NOT NULL CHECK (requested_days > 0),
    reason text,
    attachment_object_key text,
    status text NOT NULL DEFAULT 'pending'
        CHECK (status IN ('draft', 'pending', 'approved', 'rejected', 'cancelled')),
    requested_at timestamptz,
    decided_by uuid,
    decided_at timestamptz,
    decision_notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (leave_type_id)
        REFERENCES leave_mgmt.leave_type(id),
    FOREIGN KEY (decided_by)
        REFERENCES auth.user_account(id),
    CHECK (end_date >= start_date),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.leave_request_day (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    leave_request_id uuid NOT NULL,
    leave_date date NOT NULL,
    day_fraction numeric(3,2) NOT NULL CHECK (day_fraction IN (0.50, 1.00)),
    UNIQUE (leave_request_id, leave_date),
    FOREIGN KEY (leave_request_id)
        REFERENCES leave_mgmt.leave_request(id) ON DELETE CASCADE,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE leave_mgmt.leave_ledger (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    employee_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    transaction_date date NOT NULL,
    quantity numeric(8,2) NOT NULL CHECK (quantity <> 0),
    entry_type text NOT NULL
        CHECK (entry_type IN ('opening', 'accrual', 'taken', 'adjustment', 'carry_forward', 'expiry')),
    leave_request_id uuid,
    reference text,
    created_by uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (leave_type_id)
        REFERENCES leave_mgmt.leave_type(id),
    FOREIGN KEY (leave_request_id)
        REFERENCES leave_mgmt.leave_request(id),
    FOREIGN KEY (created_by)
        REFERENCES auth.user_account(id),
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE payroll.pay_component (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    component_type text NOT NULL
        CHECK (component_type IN ('earning', 'deduction', 'employer_contribution')),
    calculation_method text NOT NULL
        CHECK (calculation_method IN ('fixed', 'percentage', 'formula', 'attendance', 'overtime')),
    taxable boolean NOT NULL DEFAULT true,
    pensionable boolean NOT NULL DEFAULT false,
    display_order integer NOT NULL DEFAULT 0,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE payroll.pay_rule (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    component_id uuid NOT NULL,
    version integer NOT NULL CHECK (version > 0),
    expression text NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (component_id, version),
    FOREIGN KEY (component_id)
        REFERENCES payroll.pay_component(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_pay_rule_period
    EXCLUDE USING gist (
        component_id WITH =,
        daterange(effective_from, COALESCE(effective_to + 1, 'infinity'::date), '[)') WITH &&
    )
);

CREATE TABLE payroll.pay_rule_parameter (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    pay_rule_id uuid NOT NULL REFERENCES payroll.pay_rule(id) ON DELETE CASCADE,
    parameter_name varchar(100) NOT NULL,
    value_type text NOT NULL CHECK (value_type IN ('number', 'text', 'boolean')),
    numeric_value numeric(18,6),
    text_value text,
    boolean_value boolean,
    UNIQUE (pay_rule_id, parameter_name),
    CHECK (
        (value_type = 'number' AND numeric_value IS NOT NULL AND text_value IS NULL AND boolean_value IS NULL)
        OR (value_type = 'text' AND numeric_value IS NULL AND text_value IS NOT NULL AND boolean_value IS NULL)
        OR (value_type = 'boolean' AND numeric_value IS NULL AND text_value IS NULL AND boolean_value IS NOT NULL)
    ),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE payroll.employee_component (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_id uuid NOT NULL,
    component_id uuid NOT NULL,
    fixed_amount numeric(18,2),
    percentage numeric(9,6),
    effective_from date NOT NULL,
    effective_to date,
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (component_id)
        REFERENCES payroll.pay_component(id),
    CHECK (num_nonnulls(fixed_amount, percentage) = 1),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_employee_component_period
    EXCLUDE USING gist (
        employee_id WITH =,
        component_id WITH =,
        daterange(effective_from, COALESCE(effective_to + 1, 'infinity'::date), '[)') WITH &&
    )
);

CREATE TABLE payroll.payroll_period (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    period_code varchar(100) NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    payment_date date NOT NULL,
    status text NOT NULL DEFAULT 'open'
        CHECK (status IN ('open', 'processing', 'closed')),
    locked_at timestamptz,
    locked_by uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (period_code),
    FOREIGN KEY (locked_by)
        REFERENCES auth.user_account(id),
    CHECK (end_date >= start_date),
    CHECK (payment_date >= start_date),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_payroll_period_dates
    EXCLUDE USING gist (
        daterange(start_date, end_date + 1, '[)') WITH &&
    )
);

CREATE TABLE payroll.payroll_run (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    payroll_period_id uuid NOT NULL,
    branch_id uuid,
    run_no integer NOT NULL CHECK (run_no > 0),
    status text NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'calculating', 'calculated', 'approved', 'posted', 'cancelled')),
    calculation_version text NOT NULL,
    started_at timestamptz,
    completed_at timestamptz,
    approved_by uuid,
    approved_at timestamptz,
    posted_by uuid,
    posted_at timestamptz,
    notes text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE NULLS NOT DISTINCT (payroll_period_id, branch_id, run_no),
    FOREIGN KEY (payroll_period_id)
        REFERENCES payroll.payroll_period(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (approved_by)
        REFERENCES auth.user_account(id),
    FOREIGN KEY (posted_by)
        REFERENCES auth.user_account(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);


CREATE TABLE payroll.employee_payroll_result (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    payroll_run_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    department_id uuid NOT NULL,

    base_salary numeric(18,2) NOT NULL DEFAULT 0,
    gross_earnings numeric(18,2) NOT NULL DEFAULT 0,
    total_deductions numeric(18,2) NOT NULL DEFAULT 0,
    employer_contributions numeric(18,2) NOT NULL DEFAULT 0,
    net_pay numeric(18,2) NOT NULL DEFAULT 0,

    payable_days numeric(8,2) NOT NULL DEFAULT 0,
    worked_minutes integer NOT NULL DEFAULT 0,
    overtime_minutes integer NOT NULL DEFAULT 0,
    unpaid_leave_days numeric(8,2) NOT NULL DEFAULT 0,

    status text NOT NULL DEFAULT 'calculated'
        CHECK (status IN ('calculated', 'held', 'approved', 'posted')),

    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,

    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),

    UNIQUE (payroll_run_id, employee_id),
    UNIQUE (branch_id, id, employee_id),

    FOREIGN KEY (payroll_run_id)
        REFERENCES payroll.payroll_run(id),

    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),

    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),

    FOREIGN KEY (branch_id, department_id)
        REFERENCES core.department(branch_id, id)
);



CREATE TABLE payroll.employee_payroll_line (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    employee_payroll_result_id uuid NOT NULL,
    component_id uuid NOT NULL,
    amount numeric(18,2) NOT NULL,
    quantity numeric(18,4),
    rate numeric(18,6),
    FOREIGN KEY (employee_payroll_result_id)
        REFERENCES payroll.employee_payroll_result(id) ON DELETE CASCADE,
    FOREIGN KEY (component_id)
        REFERENCES payroll.pay_component(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE payroll.employee_payroll_line_detail (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    employee_payroll_line_id bigint NOT NULL
        REFERENCES payroll.employee_payroll_line(id) ON DELETE CASCADE,
    detail_name varchar(100) NOT NULL,
    detail_value text NOT NULL,
    UNIQUE (employee_payroll_line_id, detail_name),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE finance.payment_account (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL REFERENCES core.branch(id),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    account_type text NOT NULL CHECK (account_type IN ('cash', 'bank')),
    bank_name text,
    account_title text,
    account_number_encrypted bytea,
    iban_encrypted bytea,
    account_last4 varchar(4),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (branch_id, code),
    UNIQUE (branch_id, id),
    CHECK (
        (account_type = 'cash' AND bank_name IS NULL)
        OR (account_type = 'bank' AND bank_name IS NOT NULL)
    ),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE finance.ledger_account (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid,
    code varchar(100) NOT NULL,
    name text NOT NULL,
    account_type text NOT NULL
        CHECK (account_type IN ('asset', 'liability', 'equity', 'income', 'expense')),
    system_code varchar(100) UNIQUE,
    parent_account_id uuid REFERENCES finance.ledger_account(id),
    is_control_account boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE NULLS NOT DISTINCT (branch_id, code),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    CHECK (parent_account_id IS NULL OR parent_account_id <> id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE finance.journal_entry (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL REFERENCES core.branch(id),
    entry_no varchar(100) NOT NULL,
    posting_date date NOT NULL,
    source_type text NOT NULL
        CHECK (source_type IN ('payroll', 'salary_payment', 'loan_disbursement', 'loan_repayment', 'adjustment', 'opening')),
    source_id uuid,
    description text NOT NULL,
    status text NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'posted', 'reversed')),
    posted_by uuid REFERENCES auth.user_account(id),
    posted_at timestamptz,
    reversed_entry_id uuid REFERENCES finance.journal_entry(id),
    created_by uuid NOT NULL REFERENCES auth.user_account(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (branch_id, entry_no),
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE finance.journal_line (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    journal_entry_id uuid NOT NULL REFERENCES finance.journal_entry(id) ON DELETE CASCADE,
    line_no integer NOT NULL CHECK (line_no > 0),
    ledger_account_id uuid NOT NULL REFERENCES finance.ledger_account(id),
    employee_id uuid REFERENCES hr.employee(id),
    debit numeric(18,2) NOT NULL DEFAULT 0 CHECK (debit >= 0),
    credit numeric(18,2) NOT NULL DEFAULT 0 CHECK (credit >= 0),
    description text,
    UNIQUE (journal_entry_id, line_no),
    CHECK ((debit > 0 AND credit = 0) OR (credit > 0 AND debit = 0)),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE finance.salary_payment (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL,
    payment_no varchar(100) NOT NULL UNIQUE,
    employee_payroll_result_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    payment_date date NOT NULL,
    amount numeric(18,2) NOT NULL CHECK (amount > 0),
    payment_channel text NOT NULL CHECK (payment_channel IN ('cash', 'bank')),
    bank_payment_type text
        CHECK (bank_payment_type IS NULL OR bank_payment_type IN ('online_transfer', 'cheque')),
    payment_account_id uuid NOT NULL,
    transaction_reference text,
    cheque_number text,
    cheque_date date,
    status text NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'approved', 'paid', 'cancelled', 'reversed')),
    paid_by uuid REFERENCES auth.user_account(id),
    paid_at timestamptz,
    approved_by uuid REFERENCES auth.user_account(id),
    approved_at timestamptz,
    notes text,
    created_by uuid NOT NULL REFERENCES auth.user_account(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (branch_id, employee_payroll_result_id, employee_id)
        REFERENCES payroll.employee_payroll_result(branch_id, id, employee_id),
    FOREIGN KEY (branch_id, payment_account_id)
        REFERENCES finance.payment_account(branch_id, id),
    CHECK (
        (payment_channel = 'cash' AND bank_payment_type IS NULL)
        OR (payment_channel = 'bank' AND bank_payment_type IS NOT NULL)
    ),
    CHECK (bank_payment_type <> 'online_transfer' OR transaction_reference IS NOT NULL),
    CHECK (bank_payment_type <> 'cheque' OR (cheque_number IS NOT NULL AND cheque_date IS NOT NULL)),
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE loans.loan_type (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    interest_method text NOT NULL DEFAULT 'none'
        CHECK (interest_method IN ('none', 'flat', 'reducing_balance')),
    default_interest_rate numeric(9,6) NOT NULL DEFAULT 0 CHECK (default_interest_rate >= 0),
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE loans.loan_application (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL REFERENCES core.branch(id),
    application_no varchar(100) NOT NULL UNIQUE,
    employee_id uuid NOT NULL REFERENCES hr.employee(id),
    loan_type_id uuid NOT NULL REFERENCES loans.loan_type(id),
    application_date date NOT NULL,
    requested_amount numeric(18,2) NOT NULL CHECK (requested_amount > 0),
    requested_installment_count integer NOT NULL CHECK (requested_installment_count > 0),
    requested_installment_amount numeric(18,2) CHECK (requested_installment_amount > 0),
    purpose text NOT NULL,
    status text NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'submitted', 'approved', 'rejected', 'cancelled')),
    submitted_at timestamptz,
    approved_amount numeric(18,2) CHECK (approved_amount > 0),
    approved_installment_count integer CHECK (approved_installment_count > 0),
    approved_interest_rate numeric(9,6) CHECK (approved_interest_rate >= 0),
    decided_by uuid REFERENCES auth.user_account(id),
    decided_at timestamptz,
    decision_notes text,
    created_by uuid NOT NULL REFERENCES auth.user_account(id),
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, branch_id, employee_id, loan_type_id),
    CHECK (
        status <> 'approved'
        OR (approved_amount IS NOT NULL AND approved_installment_count IS NOT NULL)
    ),
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE loans.employee_loan (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL REFERENCES core.branch(id),
    loan_application_id uuid NOT NULL UNIQUE,
    employee_id uuid NOT NULL,
    loan_type_id uuid NOT NULL,
    loan_no varchar(100) NOT NULL,
    disbursement_date date,
    principal_amount numeric(18,2) NOT NULL CHECK (principal_amount > 0),
    interest_rate numeric(9,6) NOT NULL DEFAULT 0 CHECK (interest_rate >= 0),
    installment_amount numeric(18,2) NOT NULL CHECK (installment_amount > 0),
    installment_count integer NOT NULL CHECK (installment_count > 0),
    recovery_start_period_id uuid,
    status text NOT NULL DEFAULT 'approved'
        CHECK (status IN ('approved', 'active', 'paused', 'closed', 'cancelled')),
    pause_from date,
    pause_until date,
    approved_by uuid,
    approved_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (loan_no),
    UNIQUE (branch_id, id),
    FOREIGN KEY (loan_application_id, branch_id, employee_id, loan_type_id)
        REFERENCES loans.loan_application(id, branch_id, employee_id, loan_type_id),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (loan_type_id)
        REFERENCES loans.loan_type(id),
    FOREIGN KEY (recovery_start_period_id)
        REFERENCES payroll.payroll_period(id),
    FOREIGN KEY (approved_by)
        REFERENCES auth.user_account(id),
    CHECK (pause_until IS NULL OR pause_from IS NOT NULL),
    CHECK (pause_until IS NULL OR pause_until >= pause_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE loans.loan_schedule (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employee_loan_id uuid NOT NULL,
    installment_no integer NOT NULL CHECK (installment_no > 0),
    payroll_period_id uuid NOT NULL,
    opening_balance numeric(18,2) NOT NULL CHECK (opening_balance >= 0),
    principal_due numeric(18,2) NOT NULL DEFAULT 0 CHECK (principal_due >= 0),
    interest_due numeric(18,2) NOT NULL DEFAULT 0 CHECK (interest_due >= 0),
    amount_paid numeric(18,2) NOT NULL DEFAULT 0 CHECK (amount_paid >= 0),
    closing_balance numeric(18,2) NOT NULL CHECK (closing_balance >= 0),
    status text NOT NULL DEFAULT 'scheduled'
        CHECK (status IN ('scheduled', 'deducted', 'paid', 'skipped', 'waived')),
    payroll_line_id bigint,
    UNIQUE (employee_loan_id, installment_no),
    UNIQUE (employee_loan_id, payroll_period_id),
    FOREIGN KEY (employee_loan_id)
        REFERENCES loans.employee_loan(id),
    FOREIGN KEY (payroll_period_id)
        REFERENCES payroll.payroll_period(id),
    FOREIGN KEY (payroll_line_id)
        REFERENCES payroll.employee_payroll_line(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE loans.loan_payment (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid NOT NULL,
    employee_loan_id uuid NOT NULL,
    payment_date date NOT NULL,
    amount numeric(18,2) NOT NULL CHECK (amount > 0),
    payment_method text NOT NULL CHECK (payment_method IN ('payroll', 'cash', 'bank', 'adjustment')),
    bank_payment_type text
        CHECK (bank_payment_type IS NULL OR bank_payment_type IN ('online_transfer', 'cheque')),
    payment_account_id uuid,
    transaction_reference text,
    cheque_number text,
    cheque_date date,
    reference text,
    received_by uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (branch_id, employee_loan_id)
        REFERENCES loans.employee_loan(branch_id, id),
    FOREIGN KEY (received_by)
        REFERENCES auth.user_account(id),
    CHECK (payment_method <> 'bank' OR bank_payment_type IS NOT NULL),
    CHECK (payment_method = 'bank' OR bank_payment_type IS NULL),
    CHECK (payment_method IN ('payroll', 'adjustment') OR payment_account_id IS NOT NULL),
    CHECK (bank_payment_type <> 'online_transfer' OR transaction_reference IS NOT NULL),
    CHECK (bank_payment_type <> 'cheque' OR (cheque_number IS NOT NULL AND cheque_date IS NOT NULL)),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT fk_loan_payment_account
    FOREIGN KEY (branch_id, payment_account_id)
    REFERENCES finance.payment_account(branch_id, id)
);

CREATE TABLE benefits.subsidy_program (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    program_type text NOT NULL
        CHECK (program_type IN ('canteen', 'contractor_diesel', 'contractor_production', 'other')),
    beneficiary_type text NOT NULL
        CHECK (beneficiary_type IN ('employee', 'contractor', 'department')),
    calculation_method text NOT NULL
        CHECK (calculation_method IN ('fixed', 'quantity_rate', 'percentage')),
    is_active boolean NOT NULL DEFAULT true,
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE benefits.subsidy_rate (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    subsidy_program_id uuid NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    company_rate numeric(18,4) NOT NULL DEFAULT 0 CHECK (company_rate >= 0),
    beneficiary_rate numeric(18,4) NOT NULL DEFAULT 0 CHECK (beneficiary_rate >= 0),
    unit text NOT NULL DEFAULT 'month',
    FOREIGN KEY (subsidy_program_id)
        REFERENCES benefits.subsidy_program(id),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    CONSTRAINT ex_subsidy_rate_period
    EXCLUDE USING gist (
        subsidy_program_id WITH =,
        daterange(effective_from, COALESCE(effective_to + 1, 'infinity'::date), '[)') WITH &&
    )
);

CREATE TABLE benefits.subsidy_transaction (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    subsidy_program_id uuid NOT NULL,
    branch_id uuid NOT NULL,
    transaction_date date NOT NULL,
    employee_id uuid,
    contractor_id uuid,
    department_id uuid,
    quantity numeric(18,4) NOT NULL DEFAULT 1 CHECK (quantity > 0),
    total_amount numeric(18,2) NOT NULL CHECK (total_amount >= 0),
    company_amount numeric(18,2) NOT NULL CHECK (company_amount >= 0),
    beneficiary_amount numeric(18,2) NOT NULL CHECK (beneficiary_amount >= 0),
    payroll_period_id uuid,
    source_reference text,
    status text NOT NULL DEFAULT 'approved'
        CHECK (status IN ('draft', 'pending', 'approved', 'posted', 'cancelled')),
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (subsidy_program_id)
        REFERENCES benefits.subsidy_program(id),
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (employee_id)
        REFERENCES hr.employee(id),
    FOREIGN KEY (contractor_id, branch_id)
        REFERENCES core.contractor_branch(contractor_id, branch_id),
    FOREIGN KEY (branch_id, department_id)
        REFERENCES core.department(branch_id, id),
    FOREIGN KEY (payroll_period_id)
        REFERENCES payroll.payroll_period(id),
    CHECK (total_amount = company_amount + beneficiary_amount),
    CHECK (num_nonnulls(employee_id, contractor_id, department_id) = 1),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE workflow.approval_policy (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(100) NOT NULL,
    name text NOT NULL,
    entity_type text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (code),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE workflow.approval_policy_condition (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_policy_id uuid NOT NULL
        REFERENCES workflow.approval_policy(id) ON DELETE CASCADE,
    condition_order integer NOT NULL CHECK (condition_order > 0),
    field_name varchar(100) NOT NULL,
    comparison_operator text NOT NULL
        CHECK (comparison_operator IN ('equals', 'not_equals', 'greater_than', 'greater_or_equal', 'less_than', 'less_or_equal', 'contains')),
    comparison_value text NOT NULL,
    UNIQUE (approval_policy_id, condition_order),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE workflow.approval_policy_step (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_policy_id uuid NOT NULL,
    step_no integer NOT NULL CHECK (step_no > 0),
    approver_type text NOT NULL
        CHECK (approver_type IN ('manager', 'role', 'user')),
    approver_role_id uuid,
    approver_user_id uuid,
    minimum_approvals integer NOT NULL DEFAULT 1 CHECK (minimum_approvals > 0),
    UNIQUE (approval_policy_id, step_no),
    FOREIGN KEY (approval_policy_id)
        REFERENCES workflow.approval_policy(id) ON DELETE CASCADE,
    FOREIGN KEY (approver_role_id)
        REFERENCES auth.role(id),
    FOREIGN KEY (approver_user_id)
        REFERENCES auth.user_account(id),
    CHECK (
        (approver_type = 'manager' AND approver_role_id IS NULL AND approver_user_id IS NULL)
        OR (approver_type = 'role' AND approver_role_id IS NOT NULL AND approver_user_id IS NULL)
        OR (approver_type = 'user' AND approver_role_id IS NULL AND approver_user_id IS NOT NULL)
    ),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE workflow.approval_request (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    branch_id uuid,
    entity_type text NOT NULL,
    entity_id uuid NOT NULL,
    approval_policy_id uuid NOT NULL,
    current_step integer NOT NULL DEFAULT 1 CHECK (current_step > 0),
    status text NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'approved', 'rejected', 'cancelled')),
    requested_by uuid NOT NULL,
    requested_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    FOREIGN KEY (branch_id)
        REFERENCES core.branch(id),
    FOREIGN KEY (approval_policy_id)
        REFERENCES workflow.approval_policy(id),
    FOREIGN KEY (requested_by)
        REFERENCES auth.user_account(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE workflow.approval_action (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    approval_request_id uuid NOT NULL,
    step_no integer NOT NULL CHECK (step_no > 0),
    action text NOT NULL CHECK (action IN ('submitted', 'approved', 'rejected', 'returned', 'cancelled')),
    actor_user_id uuid NOT NULL,
    comments text,
    acted_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (approval_request_id)
        REFERENCES workflow.approval_request(id) ON DELETE CASCADE,
    FOREIGN KEY (actor_user_id)
        REFERENCES auth.user_account(id),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE audit.audit_log (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    branch_id uuid REFERENCES core.branch(id),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    actor_user_id uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    action text NOT NULL,
    entity_type text NOT NULL,
    entity_id text,
    correlation_id uuid,
    ip_address inet,
    user_agent text,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE audit.audit_log_detail (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    audit_log_id bigint NOT NULL REFERENCES audit.audit_log(id) ON DELETE CASCADE,
    column_name varchar(150) NOT NULL,
    old_value text,
    new_value text,
    UNIQUE (audit_log_id, column_name),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE audit.outbox_message (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    occurred_at timestamptz NOT NULL DEFAULT now(),
    event_type text NOT NULL,
    payload_text text NOT NULL,
    processed_at timestamptz,
    retry_count integer NOT NULL DEFAULT 0 CHECK (retry_count >= 0),
    error text,
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

CREATE TABLE audit.legacy_reference (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_system text NOT NULL,
    source_table text NOT NULL,
    source_key text NOT NULL,
    target_entity_type text NOT NULL,
    target_entity_id uuid NOT NULL,
    imported_at timestamptz NOT NULL DEFAULT now(),
    import_batch_id uuid,
    UNIQUE (source_system, source_table, source_key),
    UNIQUE (target_entity_type, target_entity_id, source_system),
    created_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL,
    updated_by uuid REFERENCES auth.user_account(id) ON DELETE SET NULL
);

-- 2. SEED / DEFAULT DATA
-- ============================================================================

INSERT INTO auth.role (code, name, description, scope_type, is_system) VALUES
    ('ADMIN', 'Admin', 'Company administrator with access to all branches', 'company', true),
    ('HR_MANAGER', 'HR Manager', 'HR and payroll operator restricted to assigned branches', 'branch', true)
ON CONFLICT (code) DO NOTHING;

-- ============================================================================
-- 3. HELPER / VALIDATION FUNCTIONS
-- ============================================================================

CREATE OR REPLACE FUNCTION core.touch_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := clock_timestamp();
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION core.normalize_business_code()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.code := upper(btrim(NEW.code));
    IF NEW.code = '' THEN
        RAISE EXCEPTION 'Business code cannot be empty';
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION finance.validate_salary_payment_total()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    payroll_net_pay numeric(18,2);
    existing_paid numeric(18,2);
BEGIN
    IF NEW.status NOT IN ('approved', 'paid') THEN
        RETURN NEW;
    END IF;

    SELECT net_pay
    INTO payroll_net_pay
    FROM payroll.employee_payroll_result
    WHERE id = NEW.employee_payroll_result_id
    FOR UPDATE;

    SELECT COALESCE(SUM(amount), 0)
    INTO existing_paid
    FROM finance.salary_payment
    WHERE employee_payroll_result_id = NEW.employee_payroll_result_id
      AND status IN ('approved', 'paid')
      AND (TG_OP = 'INSERT' OR id <> NEW.id);

    IF existing_paid + NEW.amount > payroll_net_pay THEN
        RAISE EXCEPTION 'Salary payments cannot exceed payroll net pay';
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION finance.validate_journal_posting()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    total_debit numeric(18,2);
    total_credit numeric(18,2);
BEGIN
    IF NEW.status = 'posted' AND (TG_OP = 'INSERT' OR OLD.status <> 'posted') THEN
        SELECT COALESCE(SUM(debit), 0), COALESCE(SUM(credit), 0)
        INTO total_debit, total_credit
        FROM finance.journal_line
        WHERE journal_entry_id = NEW.id;

        IF total_debit = 0 OR total_debit <> total_credit THEN
            RAISE EXCEPTION 'Journal entry must have equal non-zero debit and credit totals';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION finance.prevent_posted_journal_line_change()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    parent_status text;
    parent_id uuid;
BEGIN
    IF TG_OP = 'DELETE' THEN
        parent_id := OLD.journal_entry_id;
    ELSE
        parent_id := NEW.journal_entry_id;
    END IF;
    SELECT status INTO parent_status
    FROM finance.journal_entry
    WHERE id = parent_id;

    IF parent_status IN ('posted', 'reversed') THEN
        RAISE EXCEPTION 'Lines of a posted or reversed journal entry cannot be changed';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

-- Validate that a journal line uses a ledger account belonging to the same branch.
-- A NULL ledger_account.branch_id is treated as a company-wide/global account.
CREATE OR REPLACE FUNCTION finance.validate_journal_line_branch()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    entry_branch uuid;
    account_branch uuid;
BEGIN
    SELECT branch_id INTO entry_branch
    FROM finance.journal_entry
    WHERE id = NEW.journal_entry_id;

    SELECT branch_id INTO account_branch
    FROM finance.ledger_account
    WHERE id = NEW.ledger_account_id;

    IF account_branch IS NOT NULL AND account_branch IS DISTINCT FROM entry_branch THEN
        RAISE EXCEPTION 'Ledger account branch must match journal entry branch';
    END IF;

    RETURN NEW;
END;
$$;

-- Ensure selected payment account type matches the requested payment channel/method.
CREATE OR REPLACE FUNCTION finance.validate_salary_payment_account_type()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    selected_type text;
BEGIN
    SELECT account_type INTO selected_type
    FROM finance.payment_account
    WHERE id = NEW.payment_account_id
      AND branch_id = NEW.branch_id;

    IF selected_type IS NULL THEN
        RAISE EXCEPTION 'Payment account does not belong to the salary payment branch';
    END IF;

    IF selected_type <> NEW.payment_channel THEN
        RAISE EXCEPTION 'Payment account type (%) must match salary payment channel (%)', selected_type, NEW.payment_channel;
    END IF;

    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION finance.validate_loan_payment_account_type()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    selected_type text;
BEGIN
    IF NEW.payment_method NOT IN ('cash', 'bank') THEN
        RETURN NEW;
    END IF;

    SELECT account_type INTO selected_type
    FROM finance.payment_account
    WHERE id = NEW.payment_account_id
      AND branch_id = NEW.branch_id;

    IF selected_type IS NULL THEN
        RAISE EXCEPTION 'Payment account does not belong to the loan payment branch';
    END IF;

    IF selected_type <> NEW.payment_method THEN
        RAISE EXCEPTION 'Payment account type (%) must match loan payment method (%)', selected_type, NEW.payment_method;
    END IF;

    RETURN NEW;
END;
$$;

-- Ensure subsidy transaction beneficiary matches the configured program beneficiary type.
CREATE OR REPLACE FUNCTION benefits.validate_subsidy_beneficiary()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    expected_type text;
BEGIN
    SELECT beneficiary_type INTO expected_type
    FROM benefits.subsidy_program
    WHERE id = NEW.subsidy_program_id;

    IF expected_type = 'employee' AND NEW.employee_id IS NULL THEN
        RAISE EXCEPTION 'This subsidy program requires an employee beneficiary';
    ELSIF expected_type = 'contractor' AND NEW.contractor_id IS NULL THEN
        RAISE EXCEPTION 'This subsidy program requires a contractor beneficiary';
    ELSIF expected_type = 'department' AND NEW.department_id IS NULL THEN
        RAISE EXCEPTION 'This subsidy program requires a department beneficiary';
    END IF;

    RETURN NEW;
END;
$$;

-- Prevent overlapping leave policies for the same employee and leave type.
CREATE OR REPLACE FUNCTION leave_mgmt.prevent_employee_leave_policy_overlap()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    new_leave_type_id uuid;
BEGIN
    SELECT leave_type_id INTO new_leave_type_id
    FROM leave_mgmt.leave_policy
    WHERE id = NEW.policy_id;

    IF EXISTS (
        SELECT 1
        FROM leave_mgmt.employee_leave_policy elp
        JOIN leave_mgmt.leave_policy lp ON lp.id = elp.policy_id
        WHERE elp.employee_id = NEW.employee_id
          AND lp.leave_type_id = new_leave_type_id
          AND elp.id <> NEW.id
          AND daterange(elp.effective_from, COALESCE(elp.effective_to + 1, 'infinity'::date), '[)') &&
              daterange(NEW.effective_from, COALESCE(NEW.effective_to + 1, 'infinity'::date), '[)')
    ) THEN
        RAISE EXCEPTION 'Employee leave policies for the same leave type cannot overlap';
    END IF;

    RETURN NEW;
END;
$$;

-- Keep shift timing internally consistent.
CREATE OR REPLACE FUNCTION attendance.validate_shift_definition()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    raw_minutes integer;
    expected_minutes integer;
BEGIN
    raw_minutes := FLOOR(EXTRACT(EPOCH FROM (NEW.end_local_time - NEW.start_local_time)) / 60)::integer;

    IF NEW.crosses_midnight THEN
        IF NEW.end_local_time > NEW.start_local_time THEN
            RAISE EXCEPTION 'crosses_midnight=true is inconsistent with shift start/end times';
        END IF;
        raw_minutes := raw_minutes + 1440;
    ELSE
        IF NEW.end_local_time <= NEW.start_local_time THEN
            RAISE EXCEPTION 'crosses_midnight=false requires end_local_time > start_local_time';
        END IF;
    END IF;

    expected_minutes := raw_minutes - NEW.break_minutes;

    IF expected_minutes <= 0 THEN
        RAISE EXCEPTION 'Shift break minutes must be less than total shift duration';
    END IF;

    IF NEW.scheduled_minutes <> expected_minutes THEN
        RAISE EXCEPTION 'scheduled_minutes must equal shift duration minus break minutes (expected %)', expected_minutes;
    END IF;

    RETURN NEW;
END;
$$;

-- Preserve raw biometric facts while still allowing an unmatched punch to be mapped later.
CREATE OR REPLACE FUNCTION attendance.protect_zkt_punch_raw_fields()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'ZKT punch records cannot be deleted';
    END IF;

    IF NEW.id IS DISTINCT FROM OLD.id
       OR NEW.public_id IS DISTINCT FROM OLD.public_id
       OR NEW.branch_id IS DISTINCT FROM OLD.branch_id
       OR NEW.zkt_device_id IS DISTINCT FROM OLD.zkt_device_id
       OR NEW.zkt_user_id IS DISTINCT FROM OLD.zkt_user_id
       OR NEW.punch_time IS DISTINCT FROM OLD.punch_time
       OR NEW.punch_state IS DISTINCT FROM OLD.punch_state
       OR NEW.verify_mode IS DISTINCT FROM OLD.verify_mode
       OR NEW.work_code IS DISTINCT FROM OLD.work_code
       OR NEW.received_at IS DISTINCT FROM OLD.received_at
       OR NEW.raw_data IS DISTINCT FROM OLD.raw_data THEN
        RAISE EXCEPTION 'Raw ZKT punch fields are immutable';
    END IF;

    RETURN NEW;
END;
$$;

-- Generic immutable-row protection for accounting/audit ledgers.
CREATE OR REPLACE FUNCTION core.prevent_update_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'Rows in %.% are immutable and cannot be updated or deleted', TG_TABLE_SCHEMA, TG_TABLE_NAME;
END;
$$;

-- ============================================================================
-- 4. INDEXES
-- ============================================================================

CREATE UNIQUE INDEX ux_branch_single_head_office
    ON core.branch(is_head_office)
    WHERE is_head_office;

CREATE UNIQUE INDEX ux_user_default_branch
    ON auth.user_branch(user_id)
    WHERE is_default;

CREATE UNIQUE INDEX ux_employee_national_id_hash
    ON hr.employee (national_id_hash)
    WHERE national_id_hash IS NOT NULL;

CREATE UNIQUE INDEX ux_employee_primary_bank_account
    ON hr.employee_bank_account(employee_id)
    WHERE is_primary;

CREATE INDEX ix_zkt_punch_employee_time
    ON attendance.zkt_punch(branch_id, employee_id, punch_time);

CREATE INDEX ix_zkt_punch_unmatched
    ON attendance.zkt_punch(branch_id, zkt_user_id, punch_time)
    WHERE employee_id IS NULL;

CREATE INDEX ix_attendance_day_date_department_support
    ON attendance.attendance_day(branch_id, department_id, work_date, status);

CREATE INDEX ix_leave_request_employee_dates
    ON leave_mgmt.leave_request(branch_id, employee_id, start_date, end_date);

CREATE INDEX ix_leave_ledger_balance
    ON leave_mgmt.leave_ledger(employee_id, leave_type_id, transaction_date);

CREATE UNIQUE INDEX ux_journal_entry_source
    ON finance.journal_entry(source_type, source_id)
    WHERE source_id IS NOT NULL;

CREATE INDEX ix_salary_payment_payroll_result
    ON finance.salary_payment(employee_payroll_result_id, status);

CREATE INDEX ix_subsidy_transaction_reporting
    ON benefits.subsidy_transaction(branch_id, subsidy_program_id, transaction_date, status);

CREATE INDEX ix_audit_log_entity
    ON audit.audit_log(branch_id, entity_type, entity_id, occurred_at DESC);

CREATE INDEX ix_audit_log_correlation
    ON audit.audit_log(correlation_id) WHERE correlation_id IS NOT NULL;

CREATE INDEX ix_outbox_unprocessed
    ON audit.outbox_message(occurred_at)
    WHERE processed_at IS NULL;

-- Keep approval history but allow a completed/rejected entity to be submitted again.
CREATE UNIQUE INDEX ux_approval_request_pending
    ON workflow.approval_request(entity_type, entity_id, approval_policy_id)
    WHERE status = 'pending';

-- One approver can make only one decision on a given approval step.
CREATE UNIQUE INDEX ux_approval_action_single_decision
    ON workflow.approval_action(approval_request_id, step_no, actor_user_id)
    WHERE action IN ('approved', 'rejected');

CREATE INDEX ix_employee_assignment_current
    ON hr.employee_assignment(branch_id, employee_id, effective_from DESC);

CREATE INDEX ix_employee_compensation_current
    ON hr.employee_compensation(employee_id, effective_from DESC);

CREATE INDEX ix_employee_schedule_current
    ON attendance.employee_schedule(employee_id, effective_from DESC);

CREATE INDEX ix_payroll_result_employee
    ON payroll.employee_payroll_result(branch_id, employee_id, payroll_run_id);

CREATE INDEX ix_loan_employee_status
    ON loans.employee_loan(employee_id, status);

-- Supporting indexes for common FK lookups, approvals and reporting filters.
CREATE INDEX ix_user_role_role ON auth.user_role(role_id);

CREATE INDEX ix_user_branch_branch ON auth.user_branch(branch_id);

CREATE INDEX ix_employee_assignment_department ON hr.employee_assignment(department_id);

CREATE INDEX ix_employee_assignment_designation ON hr.employee_assignment(designation_id);

CREATE INDEX ix_employee_assignment_manager ON hr.employee_assignment(manager_employee_id) WHERE manager_employee_id IS NOT NULL;

CREATE INDEX ix_zkt_employee_mapping_employee ON attendance.zkt_employee_mapping(employee_id);

CREATE INDEX ix_adjustment_request_employee_date_status ON attendance.adjustment_request(employee_id, work_date, status);

CREATE INDEX ix_overtime_request_branch_status ON attendance.overtime_request(branch_id, status);

CREATE INDEX ix_leave_request_branch_status ON leave_mgmt.leave_request(branch_id, status);

CREATE INDEX ix_leave_request_day_date ON leave_mgmt.leave_request_day(leave_date);

CREATE INDEX ix_employee_payroll_line_component ON payroll.employee_payroll_line(component_id);

CREATE INDEX ix_approval_action_request_step ON workflow.approval_action(approval_request_id, step_no, acted_at);

-- ============================================================================
-- 5. EXPLICIT TRIGGERS
-- ============================================================================

CREATE TRIGGER trg_validate_salary_payment_total
BEFORE INSERT OR UPDATE OF amount, status
ON finance.salary_payment
FOR EACH ROW EXECUTE FUNCTION finance.validate_salary_payment_total();

CREATE TRIGGER trg_validate_journal_posting
BEFORE INSERT OR UPDATE OF status
ON finance.journal_entry
FOR EACH ROW EXECUTE FUNCTION finance.validate_journal_posting();

CREATE TRIGGER trg_prevent_posted_journal_line_change
BEFORE INSERT OR UPDATE OR DELETE
ON finance.journal_line
FOR EACH ROW EXECUTE FUNCTION finance.prevent_posted_journal_line_change();

CREATE TRIGGER trg_validate_journal_line_branch
BEFORE INSERT OR UPDATE OF journal_entry_id, ledger_account_id
ON finance.journal_line
FOR EACH ROW EXECUTE FUNCTION finance.validate_journal_line_branch();

CREATE TRIGGER trg_validate_salary_payment_account_type
BEFORE INSERT OR UPDATE OF branch_id, payment_account_id, payment_channel
ON finance.salary_payment
FOR EACH ROW EXECUTE FUNCTION finance.validate_salary_payment_account_type();

CREATE TRIGGER trg_validate_loan_payment_account_type
BEFORE INSERT OR UPDATE OF branch_id, payment_account_id, payment_method
ON loans.loan_payment
FOR EACH ROW EXECUTE FUNCTION finance.validate_loan_payment_account_type();

CREATE TRIGGER trg_validate_subsidy_beneficiary
BEFORE INSERT OR UPDATE OF subsidy_program_id, employee_id, contractor_id, department_id
ON benefits.subsidy_transaction
FOR EACH ROW EXECUTE FUNCTION benefits.validate_subsidy_beneficiary();

CREATE TRIGGER trg_prevent_employee_leave_policy_overlap
BEFORE INSERT OR UPDATE OF employee_id, policy_id, effective_from, effective_to
ON leave_mgmt.employee_leave_policy
FOR EACH ROW EXECUTE FUNCTION leave_mgmt.prevent_employee_leave_policy_overlap();

CREATE TRIGGER trg_validate_shift_definition
BEFORE INSERT OR UPDATE OF start_local_time, end_local_time, break_minutes, crosses_midnight, scheduled_minutes
ON attendance.shift
FOR EACH ROW EXECUTE FUNCTION attendance.validate_shift_definition();

CREATE TRIGGER trg_protect_zkt_punch_raw_fields
BEFORE UPDATE OR DELETE
ON attendance.zkt_punch
FOR EACH ROW EXECUTE FUNCTION attendance.protect_zkt_punch_raw_fields();

CREATE TRIGGER trg_leave_ledger_immutable
BEFORE UPDATE OR DELETE ON leave_mgmt.leave_ledger
FOR EACH ROW EXECUTE FUNCTION core.prevent_update_delete();

CREATE TRIGGER trg_audit_log_immutable
BEFORE UPDATE OR DELETE ON audit.audit_log
FOR EACH ROW EXECUTE FUNCTION core.prevent_update_delete();

CREATE TRIGGER trg_audit_log_detail_immutable
BEFORE UPDATE OR DELETE ON audit.audit_log_detail
FOR EACH ROW EXECUTE FUNCTION core.prevent_update_delete();

-- ============================================================================
-- 6. BULK HELPER TRIGGER SETUP
-- ============================================================================

DO $$
DECLARE
    target regclass;
BEGIN
    FOREACH target IN ARRAY ARRAY[
        'core.company_profile'::regclass,
        'core.branch'::regclass,
        'core.department'::regclass,
        'core.designation'::regclass,
        'core.contractor'::regclass,
        'auth.user_account'::regclass,
        'hr.employee'::regclass,
        'hr.employee_bank_account'::regclass,
        'attendance.shift'::regclass,
        'attendance.attendance_policy'::regclass,
        'attendance.zkt_device'::regclass,
        'attendance.zkt_employee_mapping'::regclass,
        'attendance.attendance_day'::regclass,
        'attendance.adjustment_request'::regclass,
        'attendance.overtime_request'::regclass,
        'leave_mgmt.leave_request'::regclass,
        'payroll.payroll_period'::regclass,
        'payroll.payroll_run'::regclass,
        'loans.loan_application'::regclass,
        'loans.employee_loan'::regclass,
        'finance.payment_account'::regclass,
        'finance.ledger_account'::regclass,
        'finance.salary_payment'::regclass,
        'workflow.approval_policy'::regclass
    ]
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_touch_updated_at BEFORE UPDATE ON %s '
            'FOR EACH ROW EXECUTE FUNCTION core.touch_updated_at()',
            target
        );
    END LOOP;
END;
$$;

DO $$
DECLARE
    target regclass;
BEGIN
    FOREACH target IN ARRAY ARRAY[
        'core.branch'::regclass,
        'core.department'::regclass,
        'core.designation'::regclass,
        'core.contractor'::regclass,
        'auth.role'::regclass,
        'attendance.holiday_calendar'::regclass,
        'attendance.shift'::regclass,
        'attendance.attendance_policy'::regclass,
        'attendance.zkt_device'::regclass,
        'leave_mgmt.leave_type'::regclass,
        'payroll.pay_component'::regclass,
        'loans.loan_type'::regclass,
        'finance.payment_account'::regclass,
        'finance.ledger_account'::regclass,
        'benefits.subsidy_program'::regclass,
        'workflow.approval_policy'::regclass
    ]
    LOOP
        EXECUTE format(
            'CREATE TRIGGER trg_normalize_business_code BEFORE INSERT OR UPDATE OF code ON %s '
            'FOR EACH ROW EXECUTE FUNCTION core.normalize_business_code()',
            target
        );
    END LOOP;
END;
$$;

-- ============================================================================
-- 7. REPORTING VIEWS
-- ============================================================================

CREATE VIEW reporting.v_salary_payment_status AS
SELECT
    epr.id AS employee_payroll_result_id,
    epr.branch_id,
    epr.department_id,
    epr.employee_id,
    epr.net_pay,
    COALESCE(SUM(sp.amount) FILTER (WHERE sp.status = 'paid'), 0) AS paid_amount,
    epr.net_pay - COALESCE(SUM(sp.amount) FILTER (WHERE sp.status = 'paid'), 0) AS outstanding_amount
FROM payroll.employee_payroll_result epr
LEFT JOIN finance.salary_payment sp
  ON sp.employee_payroll_result_id = epr.id
GROUP BY epr.id, epr.branch_id, epr.department_id, epr.employee_id, epr.net_pay;

CREATE VIEW reporting.v_employee_ledger AS
SELECT
    jl.employee_id,
    je.branch_id,
    je.posting_date,
    je.entry_no,
    je.source_type,
    je.source_id,
    la.code AS ledger_code,
    la.name AS ledger_name,
    jl.debit,
    jl.credit,
    SUM(jl.debit - jl.credit) OVER (
        PARTITION BY jl.employee_id
        ORDER BY je.posting_date, je.entry_no, jl.line_no
    ) AS running_balance
FROM finance.journal_line jl
JOIN finance.journal_entry je ON je.id = jl.journal_entry_id
JOIN finance.ledger_account la ON la.id = jl.ledger_account_id
WHERE je.status = 'posted' AND jl.employee_id IS NOT NULL;

CREATE VIEW reporting.v_unbalanced_journal_entry AS
SELECT
    je.id AS journal_entry_id,
    je.branch_id,
    je.entry_no,
    SUM(jl.debit) AS total_debit,
    SUM(jl.credit) AS total_credit
FROM finance.journal_entry je
JOIN finance.journal_line jl ON jl.journal_entry_id = je.id
GROUP BY je.id, je.branch_id, je.entry_no
HAVING SUM(jl.debit) <> SUM(jl.credit);

CREATE VIEW hr.v_employee_current_assignment AS
SELECT
    e.id AS employee_id,
    e.user_account_id,
    e.employee_no,
    e.payroll_no,
    e.first_name,
    e.last_name,
    e.employment_status,
    a.branch_id,
    b.code AS branch_code,
    b.name AS branch_name,
    a.department_id,
    d.code AS department_code,
    d.name AS department_name,
    a.designation_id,
    des.code AS designation_code,
    des.name AS designation_name,
    a.manager_employee_id,
    a.effective_from
FROM hr.employee e
LEFT JOIN LATERAL (
    SELECT ea.*
    FROM hr.employee_assignment ea
    WHERE ea.employee_id = e.id
      AND ea.effective_from <= current_date
      AND (ea.effective_to IS NULL OR ea.effective_to >= current_date)
    ORDER BY ea.effective_from DESC
    LIMIT 1
) a ON true
LEFT JOIN core.branch b ON b.id = a.branch_id
LEFT JOIN core.department d ON d.id = a.department_id
LEFT JOIN core.designation des ON des.id = a.designation_id;

CREATE VIEW reporting.v_leave_balance AS
SELECT
    employee_id,
    leave_type_id,
    SUM(quantity) AS balance
FROM leave_mgmt.leave_ledger
GROUP BY employee_id, leave_type_id;

CREATE VIEW reporting.v_monthly_attendance AS
SELECT
    branch_id,
    department_id,
    employee_id,
    date_trunc('month', work_date)::date AS month_start,
    COUNT(*) FILTER (WHERE status IN ('present', 'late')) AS present_days,
    COUNT(*) FILTER (WHERE status = 'absent') AS absent_days,
    COUNT(*) FILTER (WHERE status = 'late') AS late_days,
    COUNT(*) FILTER (WHERE status = 'leave') AS leave_days,
    COUNT(*) FILTER (WHERE status = 'holiday') AS holidays,
    SUM(worked_minutes) AS worked_minutes,
    SUM(overtime_minutes) AS overtime_minutes,
    SUM(early_out_minutes) AS early_out_minutes
FROM attendance.attendance_day
GROUP BY branch_id, department_id, employee_id, date_trunc('month', work_date)::date;

CREATE VIEW reporting.v_payroll_summary AS
SELECT
    pp.period_code,
    pp.start_date,
    pp.end_date,
    pr.id AS payroll_run_id,
    epr.branch_id,
    epr.department_id,
    epr.employee_id,
    epr.gross_earnings,
    epr.total_deductions,
    epr.employer_contributions,
    epr.net_pay,
    epr.status
FROM payroll.employee_payroll_result epr
JOIN payroll.payroll_run pr
  ON pr.id = epr.payroll_run_id
JOIN payroll.payroll_period pp
  ON pp.id = pr.payroll_period_id;

-- ============================================================================
-- COMMIT
-- ============================================================================

-- Concurrency: map PostgreSQL xmin as an EF Core concurrency token on mutable tables.
-- Use least-privilege database roles. Keep background payroll jobs under a separate restricted role.

COMMIT;
