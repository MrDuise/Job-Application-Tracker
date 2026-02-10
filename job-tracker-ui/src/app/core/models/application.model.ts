export enum ApplicationStatus {
  Applied = 'Applied',
  UnderReview = 'UnderReview',
  InterviewScheduled = 'InterviewScheduled',
  TechnicalAssessment = 'TechnicalAssessment',
  Offer = 'Offer',
  Rejected = 'Rejected',
  Withdrawn = 'Withdrawn',
}

export interface Application {
  id: number;
  companyName: string;
  jobTitle: string;
  status: ApplicationStatus;
  appliedDate: string;
  lastUpdated?: string;
  notes?: string;
  emailCount: number;
}

export interface CreateApplication {
  companyName: string;
  jobTitle: string;
  status?: ApplicationStatus;
  appliedDate?: string;
  notes?: string;
}

export interface UpdateApplication {
  companyName?: string;
  jobTitle?: string;
  status?: ApplicationStatus;
  notes?: string;
}

export interface DashboardStats {
  totalApplications: number;
  statusCounts: Record<string, number>;
  recentActivity: RecentActivity[];
}

export interface RecentActivity {
  applicationId: number;
  companyName: string;
  jobTitle: string;
  status: string;
  date: string;
}

export interface EmailAccount {
  emailAddress: string;
  imapServer: string;
  imapPort: number;
  username: string;
  password: string;
}
